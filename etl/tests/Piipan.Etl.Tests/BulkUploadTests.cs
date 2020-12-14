using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
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
                writer.WriteLine("last,first,middle,dob,ssn,exception");
            }
            foreach (var record in records)
            {
                writer.WriteLine(record);
            }
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        static PiiRecord RecordFixture()
        {
            return new PiiRecord
            {
                Last = "Last",
                First = "First",
                Middle = "Middle",
                Dob = new DateTime(1970, 1, 1),
                Ssn = "000-00-0000",
                Exception = "Exception"
            };
        }

        [Fact]
        public void ReadAllFields()
        {
            var logger = Mock.Of<ILogger>();
            var stream = CsvFixture(new string[] {
                "Last,First,Middle,01/01/1970,000-00-0000,Exception"
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
            }
        }

        [Fact]
        public void ReadOptionalFields()
        {
            var logger = Mock.Of<ILogger>();
            var stream = CsvFixture(new string[] {
                "Last,,,01/01/1970,000-00-0000,"
            });

            var records = BulkUpload.Read(stream, logger);
            foreach (var record in records)
            {
                Assert.Null(record.First);
                Assert.Null(record.Middle);
                Assert.Null(record.Exception);
            }
        }

        [Theory]
        [InlineData(",,,01/01/1970,000-00-0000,")] // Missing last name
        [InlineData("Last,,,01/01/1970,,")] // Missing SSN
        [InlineData("Last,,,01/01/1970,000000000,")] // Malformed SSN
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
        public void CountInserts()
        {
            var logger = Mock.Of<ILogger>();
            var factory = new Mock<DbProviderFactory>() { DefaultValue = DefaultValue.Mock };
            var cmd = new Mock<DbCommand>() { DefaultValue = DefaultValue.Mock };
            factory.Setup(f => f.CreateCommand()).Returns(cmd.Object);

            // Mocks foreign key used in participants table
            cmd.Setup(c => c.ExecuteScalar()).Returns((Int64)1);

            // Mock can't test unique constraint on SSN
            var records = new List<PiiRecord>() {
                RecordFixture(),
                RecordFixture(),
            };
            BulkUpload.Load(records, factory.Object, logger);

            // Row in uploads table + rows in participants table
            cmd.Verify(f => f.ExecuteNonQuery(), Times.Exactly(1 + records.Count));
        }
    }
}
