using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Piipan.Shared.Authentication;
using Xunit;

namespace Piipan.Match.Orchestrator.Tests
{
    public class ApiTests
    {
        static PiiRecord FullRecord()
        {
            return new PiiRecord
            {
                First = "First",
                Middle = "Middle",
                Last = "Last",
                Dob = new DateTime(1970, 1, 1),
                Ssn = "000-00-0000",
                Exception = "Exception",
                CaseId = "CaseIdExample"
            };
        }

        static MatchQueryRequest FullRequest()
        {
            return new MatchQueryRequest
            {
                Query = new MatchQuery
                {
                    First = "First",
                    Middle = "Middle",
                    Last = "Last",
                    Dob = new DateTime(1970, 1, 1),
                    Ssn = "000-00-0000"
                }
            };
        }

        static MatchQueryResponse FullResponse()
        {
            return new MatchQueryResponse
            {
                Matches = new List<PiiRecord> { FullRecord() }
            };
        }

        static String JsonBody(string json)
        {
            var data = new
            {
                query = JsonConvert.DeserializeObject(json)
            };

            return JsonConvert.SerializeObject(data);
        }

        static Mock<HttpRequest> MockRequest(string jsonBody)
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);

            sw.Write(jsonBody);
            sw.Flush();

            ms.Position = 0;

            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Body).Returns(ms);

            return mockRequest;
        }

        static Mock<HttpMessageHandler> MockMessageHandler(HttpStatusCode status, string response)
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                })
                .Verifiable();

            return mockHttpMessageHandler;
        }

        static Mock<ITokenProvider> MockTokenProvider(string value)
        {
            var token = new AccessToken(value, DateTimeOffset.Now);
            var mockTokenProvider = new Mock<ITokenProvider>();
            mockTokenProvider
                .Setup(t => t.RetrieveAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(token));

            return mockTokenProvider;
        }

        public static Mock<ITableStorage<QueryEntity>> MockLookupStorage()
        {
            var mockQuery = FullRequest();
            var mockLookupStorage = new Mock<ITableStorage<QueryEntity>>();

            // Attempts to store a lookup ID should result in a QueryEntity
            mockLookupStorage
                .Setup(ts => ts.InsertAsync(It.IsAny<QueryEntity>()))
                .Returns<QueryEntity>(e => Task.FromResult(e));

            // Attempts to retrieve QueryEntity should result in a QueryEntity
            mockLookupStorage
                .Setup(ts => ts.PointQueryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((pk, rk) =>
                {
                    var qe = new QueryEntity(pk, rk);
                    qe.Body = mockQuery.Query.ToJson();
                    return Task.FromResult(qe);
                });

            return mockLookupStorage;
        }

        static Api Construct()
        {
            var client = new HttpClient();
            var tokenProvider = new EasyAuthTokenProvider();
            var apiClient = new AuthorizedJsonApiClient(client, tokenProvider);
            var lookupStorage = Mock.Of<ITableStorage<QueryEntity>>();
            var api = new Api(apiClient, lookupStorage);

            return api;
        }

        static Api ConstructMocked(Mock<HttpMessageHandler> handler)
        {
            var mockTokenProvider = MockTokenProvider("|token|");
            var client = new HttpClient(handler.Object);
            var apiClient = new AuthorizedJsonApiClient(client, mockTokenProvider.Object);
            var lookupStorage = MockLookupStorage();

            var api = new Api(apiClient, lookupStorage.Object);

            return api;
        }

        ////
        // Tests
        ////

        [Fact]
        public void PiiRecordJson()
        {
            var json = @"{last: 'Last', first: 'First', dob: '2020-01-01', ssn: '000000000', case_id: 'foo'}";
            var record = JsonConvert.DeserializeObject<PiiRecord>(json);

            Assert.Contains("\"last\": \"Last\"", record.ToJson());
            Assert.Contains("\"dob\": \"2020-01-01\"", record.ToJson());
            Assert.Contains("\"ssn\": \"000000000\"", record.ToJson());
            Assert.Contains("\"first\": \"First\"", record.ToJson());
            Assert.Contains("\"middle\": null", record.ToJson());
            Assert.Contains("\"exception\": null", record.ToJson());
            Assert.Contains("\"state_name\": null", record.ToJson());
            Assert.Contains("\"state_abbr\": null", record.ToJson());
            Assert.Contains("\"case_id\": \"foo\"", record.ToJson());

        }

        // Malformed data results in BadRequest
        [Theory]
        [InlineData("{{")]
        [InlineData("<xml>")]
        public async void ExpectMalformedDataResultsInBadRequest(string query)
        {
            // Arrange
            var api = Construct();
            Mock<HttpRequest> mockRequest = MockRequest(query);
            var logger = Mock.Of<ILogger>();

            // Act
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            Assert.IsType<BadRequestResult>(response);
            var result = response as BadRequestResult;
            Assert.Equal(400, result.StatusCode);
        }

        // Invalid data results in BadRequest
        [Theory]
        [InlineData(@"{last: 'Last', dob: '2020-01-01', ssn: '000-00-000'}")] // Invalid Ssn format
        [InlineData(@"{last: '', dob: '2020-01-01', ssn: '000-00-0000'}")] // Empty last
        [InlineData(@"{last: '        ', dob: '2020-01-01', ssn: '000-00-0000'}")] // Whitespace last
        [InlineData(@"{last: 'Last', first: '', dob: '2020-01-01', ssn: '000-00-000'}")] // Empty first
        [InlineData(@"{last: 'Last', first: '       ', dob: '2020-01-01', ssn: '000-00-000'}")] // Whitespace first
        [InlineData(@"{last: 'Last', dob: '2020-01-01', ssn: '000000000'}")] // Invalid Ssn format
        public async void ExpectBadResultFromInvalidData(string query)
        {
            // Arrange
            var api = Construct();
            Mock<HttpRequest> mockRequest = MockRequest(JsonBody(query));
            var logger = Mock.Of<ILogger>();

            // Act
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            Assert.IsType<BadRequestResult>(response);
            var result = response as BadRequestResult;
            Assert.Equal(400, result.StatusCode);
        }

        // Incomplete data results in BadRequest
        [Theory]
        [InlineData("")]
        [InlineData(@"{first: 'First'}")] // Missing Last, Dob, and Ssn
        [InlineData(@"{last: 'Last'}")] // Missing Dob and Ssn
        [InlineData(@"{last: 'Last', dob: '2020-01-01'}")] // Missing Ssn
        [InlineData(@"{last: 'Last', dob: '2020-01-1', ssn: '000-00-000'}")] // Invalid Dob DateTime
        [InlineData(@"{last: 'Last', dob: '', ssn: '000-00-000'}")] // Empty Dob DateTime
        [InlineData(@"{last: 'Last', dob: '2020-01-01', ssn: '000-00-000'}")] // Missing First
        public async void ExpectBadResultFromIncompleteData(string query)
        {
            // Arrange
            var api = Construct();
            Mock<HttpRequest> mockRequest = MockRequest(JsonBody(query));
            var logger = Mock.Of<ILogger>();

            // Act
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            Assert.IsType<BadRequestResult>(response);
            var result = response as BadRequestResult;
            Assert.Equal(400, result.StatusCode);
        }

        // Successful API call
        [Fact]
        public async void SuccessfulApiCall()
        {
            // Arrange Mocks
            var logger = Mock.Of<ILogger>();
            var mockRequest = MockRequest(FullRequest().ToJson());
            var mockHandler = MockMessageHandler(HttpStatusCode.OK, FullResponse().ToJson());

            // Arrage Environment
            var uriString = "[\"https://localhost/\"]";
            Environment.SetEnvironmentVariable("StateApiUriStrings", uriString);

            // Act
            var api = ConstructMocked(mockHandler);
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            Assert.IsType<JsonResult>(response);

            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        // Failed state API call results in InternalServerError
        [Fact]
        public async void FailedStateCall()
        {
            // Arrange Mocks
            var logger = Mock.Of<ILogger>();
            var mockRequest = MockRequest(FullRequest().ToJson());
            var mockHandler = MockMessageHandler(HttpStatusCode.InternalServerError, "");

            // Arrage Environment
            var uriString = "[\"https://localhost/\"]";
            Environment.SetEnvironmentVariable("StateApiUriStrings", uriString);

            // Act
            var api = ConstructMocked(mockHandler);
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            Assert.IsType<InternalServerErrorResult>(response);
        }

        // Required services are passed to Api on startup
        [Fact]
        public void DependencyInjection()
        {
            var startup = new Startup();
            var host = new HostBuilder()
                .ConfigureWebJobs(startup.Configure)
                .Build();

            Assert.NotNull(host);
            Assert.NotNull(host.Services.GetRequiredService<IAuthorizedApiClient>());
        }
    }
}
