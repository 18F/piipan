using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Piipan.Etl.Tests
{
    public class BulkUploadTests
    {
        static Stream CsvFixture(string[] records, bool includeHeader = true)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            if (includeHeader)
            {
                writer.WriteLine("last,first,middle,dob,ssn,exception,case id,participant id, benefits end month, recent benefit months");
            }
            foreach (var record in records)
            {
                writer.WriteLine(record);
            }
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        static Stream BadBlob()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.WriteLine("foo");
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        static PiiRecord AllFields()
        {
            return new PiiRecord
            {
                Last = "Last",
                First = "First",
                Middle = "Middle",
                Dob = new DateTime(1970, 1, 1),
                Ssn = "000-00-0000",
                Exception = "Exception",
                CaseId = "CaseId",
                ParticipantId = "ParticipantId",
                BenefitsEndDate = new DateTime(1970, 1, 1),
                RecentBenefitMonths = "2021-05 2021-04"
            };
        }

        static PiiRecord OnlyRequiredFields()
        {
            return new PiiRecord
            {
                Last = "Last",
                First = null,
                Middle = null,
                Dob = new DateTime(1970, 1, 1),
                Ssn = "000-00-0000",
                Exception = null,
                CaseId = "CaseId",
                ParticipantId = null,
                BenefitsEndDate = null,
                RecentBenefitMonths = null
            };
        }

        static EventGridEvent EventMock()
        {
            var e = Mock.Of<EventGridEvent>();
            // Can't override Data in Setup, just use a real one
            e.Data = new Object();
            return e;
        }

        // Check that the expected message was logged as an error at least once
        static void VerifyLogError(Mock<ILogger> logger, String expected)
        {
            logger.Verify(x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == expected),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
            ));
        }

        [Fact]
        public void ReadAllFields()
        {
            var logger = Mock.Of<ILogger>();
            var stream = CsvFixture(new string[] {
                "Last,First,Middle,01/01/1970,000-00-0000,Exception,CaseId,ParticipantId,01/1970,2021-05 2021-04"
            });

            var records = BulkUpload.Read(stream, logger);
            foreach (var record in records)
            {
                Assert.Equal("Last", record.Last);
                Assert.Equal("First", record.First);
                Assert.Equal("Middle", record.Middle);
                Assert.Equal(new DateTime(1970, 1, 1), record.Dob);
                Assert.Equal("000-00-0000", record.Ssn);
                Assert.Equal("Exception", record.Exception);
                Assert.Equal("CaseId", record.CaseId);
                Assert.Equal("ParticipantId", record.ParticipantId);
                Assert.Equal(new DateTime(1970, 1, 1), record.BenefitsEndDate);
                Assert.Equal("2021-05 2021-04", record.RecentBenefitMonths);
            }
        }

        [Fact]
        public void ReadOptionalFields()
        {
            var logger = Mock.Of<ILogger>();
            var stream = CsvFixture(new string[] {
                "Last,,,01/01/1970,000-00-0000,,CaseId,,,"
            });

            var records = BulkUpload.Read(stream, logger);
            foreach (var record in records)
            {
                Assert.Null(record.First);
                Assert.Null(record.Middle);
                Assert.Null(record.Exception);
                Assert.Null(record.ParticipantId);
                Assert.Null(record.BenefitsEndDate);
            }
        }

        [Theory]
        [InlineData(",,,01/01/1970,000-00-0000,")] // Missing last name
        [InlineData("Last,,,01/01/1970,,")] // Missing SSN
        [InlineData("Last,,,01/01/1970,000000000,")] // Malformed SSN
        [InlineData("Last,,,01/01/1970,000-00-0000,,,")] // Missing CaseId
        public void ExpectFieldValidationError(String inline)
        {
            var logger = Mock.Of<ILogger>();
            var stream = CsvFixture(new string[] { inline });

            var records = BulkUpload.Read(stream, logger);
            Assert.Throws<CsvHelper.FieldValidationException>(() =>
            {
                foreach (var record in records)
                {
                    ;
                }
            });
        }

        [Theory]
        [InlineData("Last,,,,000-00-0000,")] // Missing DOB
        [InlineData("Last,,,02/31/1970,000-00-0000,")] // Invalid DOB
        public void ExpectReadErrror(String inline)
        {
            var logger = Mock.Of<ILogger>();
            var stream = CsvFixture(new string[] { inline });

            var records = BulkUpload.Read(stream, logger);
            Assert.Throws<CsvHelper.ReaderException>(() =>
            {
                foreach (var record in records)
                {
                    ;
                }
            });
        }

        [Fact]
        public async void CountInserts()
        {
            var logger = Mock.Of<ILogger>();
            var factory = new Mock<DbProviderFactory>() { DefaultValue = DefaultValue.Mock };
            var cmd = new Mock<DbCommand>() { DefaultValue = DefaultValue.Mock };
            factory.Setup(f => f.CreateCommand()).Returns(cmd.Object);

            // Mocks foreign key used in participants table
            cmd.Setup(c => c.ExecuteScalar()).Returns((Int64)1);

            // Mock can't test unique constraint on SSN
            var records = new List<PiiRecord>() {
                AllFields(),
                OnlyRequiredFields(),
            };
            await BulkUpload.Load(records, factory.Object, logger);

            // Row in uploads table + rows in participants table
            cmd.Verify(f => f.ExecuteNonQuery(), Times.Exactly(1 + records.Count));
        }

        [Fact]
        public async void NoInputStream()
        {
            var gridEvent = EventMock();
            var logger = new Mock<ILogger>();

            Stream input = null;
            await BulkUpload.Run(gridEvent, input, logger.Object);
            VerifyLogError(logger, "No input stream was provided");
        }

        [Fact]
        public async void BadInputStream()
        {
            var gridEvent = EventMock();
            var logger = new Mock<ILogger>();

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await BulkUpload.Run(gridEvent, BadBlob(), logger.Object);
            });
        }

        [Fact]
        public void LastDayOfMonth()
        {

          var monthWith31Days = new DateTime(1970,1,1);
          Assert.Equal(31, BulkUpload.LastDayOfMonth(monthWith31Days).Day);
          var monthWith30Days = new DateTime(1970,4,1);
          Assert.Equal(30, BulkUpload.LastDayOfMonth(monthWith30Days).Day);
          var februaryLeapYear = new DateTime(2000,2,1);
          Assert.Equal(29, BulkUpload.LastDayOfMonth(februaryLeapYear).Day);
          var februaryNonLeapYear = new DateTime(2001,2,1);
          Assert.Equal(28, BulkUpload.LastDayOfMonth(februaryNonLeapYear).Day);
        }

        [Fact]
        public void FormatDatesAsPgArray()
        {
          var emptyString = string.Empty;
          Assert.Equal("{}", BulkUpload.FormatDatesAsPgArray(emptyString));
          var singleDate = "2021-05";
          Assert.Equal("{2021-05-01}", BulkUpload.FormatDatesAsPgArray(singleDate));
          var multiDates = "2021-05 2021-04";
          Assert.Equal("{2021-05-01,2021-04-01}", BulkUpload.FormatDatesAsPgArray(multiDates));
        }
    }
}
