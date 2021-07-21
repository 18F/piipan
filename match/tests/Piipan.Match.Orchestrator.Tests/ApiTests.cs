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
using Piipan.Match.Shared;
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
                CaseId = "CaseIdExample",
                BenefitsEndMonth = new DateTime(1970, 1, 31),
                RecentBenefitMonths = new List<DateTime>() {
                  new DateTime(2021, 5, 31),
                  new DateTime(2021, 4, 30),
                  new DateTime(2021, 3, 31)
                },
                ProtectLocation = true
            };
        }

        static OrchMatchRequest FullRequest()
        {
            return new OrchMatchRequest
            {
                Data = new List<RequestPerson>() {
                    new RequestPerson
                    {
                        First = "First",
                        Middle = "Middle",
                        Last = "Last",
                        Dob = new DateTime(1970, 1, 1),
                        Ssn = "000-00-0000"
                    }
                }
            };
        }

        static OrchMatchRequest FullRequestMultiple()
        {
            return new OrchMatchRequest
            {
                Data = new List<RequestPerson>() {
                    new RequestPerson
                    {
                        First = "First",
                        Middle = "Middle",
                        Last = "Last",
                        Dob = new DateTime(1970, 1, 1),
                        Ssn = "000-00-0000"
                    },
                    new RequestPerson
                    {
                        First = "FirstTwo",
                        Middle = "MiddleTwo",
                        Last = "LastTwo",
                        Dob = new DateTime(1970, 1, 2),
                        Ssn = "000-00-0001"
                    }
                }
            };
        }

        static OrchMatchRequest OverMaxRequest() {
            var list = new List<RequestPerson>();
            for (int i = 0; i < 51; i++)
            {
                list.Add(new RequestPerson
                {
                    First = "First",
                    Middle = "Middle",
                    Last = "Last",
                    Dob = new DateTime(1970, 1, 1),
                    Ssn = "000-00-0000"
                });
            }
            return new OrchMatchRequest { Data = list };
        }

        static OrchMatchResult StateResponse()
        {
            var stateResponse = new OrchMatchResult
            {
                Index = 0,
                Matches = new List<PiiRecord> { FullRecord() }
            };
            return stateResponse;
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

        static HttpResponseMessage MockResponse(System.Net.HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage
            {
              StatusCode = statusCode,
              Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        static Mock<HttpMessageHandler> MockMessageHandler(List<HttpResponseMessage> responses)
        {
            var responseQueue = new Queue<HttpResponseMessage>();
            foreach (HttpResponseMessage response in responses)
            {
                responseQueue.Enqueue(response);
            }
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseQueue.Dequeue)
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
                    qe.Body = mockQuery.Data[0].ToJson();
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
            var json = @"{last: 'Last', first: 'First', dob: '2020-01-01', ssn: '000000000', case_id: 'foo', benefits_end_month: '2020-01', recent_benefit_months: ['2019-12', '2019-11', '2019-10'], protect_location: true}";
            var record = JsonConvert.DeserializeObject<PiiRecord>(json);

            string jsonRecord = record.ToJson();

            Assert.Contains("\"last\": \"Last\"", jsonRecord);
            Assert.Contains("\"dob\": \"2020-01-01\"", jsonRecord);
            Assert.Contains("\"ssn\": \"000000000\"", jsonRecord);
            Assert.Contains("\"first\": \"First\"", jsonRecord);
            Assert.Contains("\"middle\": null", jsonRecord);
            Assert.Contains("\"exception\": null", jsonRecord);
            Assert.Contains("\"state\": null", jsonRecord);
            Assert.Contains("\"case_id\": \"foo\"", jsonRecord);
            Assert.Contains("\"benefits_end_month\": \"2020-01\"", jsonRecord);
            Assert.Contains("\"recent_benefit_months\": [", jsonRecord);
            Assert.Contains("\"2019-12\",", jsonRecord);
            Assert.Contains("\"2019-11\",", jsonRecord);
            Assert.Contains("\"2019-10\"", jsonRecord);
            Assert.Contains("\"protect_location\": true", jsonRecord);

            // Deprecated
            Assert.Contains("\"state_abbr\": null", record.ToJson());
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
            var result = response as BadRequestObjectResult;
            Assert.Equal(400, result.StatusCode);

            var errorResponse = result.Value as ApiErrorResponse;
            Assert.Equal(1, (int)errorResponse.Errors.Count);
            Assert.Equal("400", errorResponse.Errors[0].Status);
            Assert.NotEmpty(errorResponse.Errors[0].Title);
            Assert.NotEmpty(errorResponse.Errors[0].Detail);
        }

        // Invalid data results in BadRequest
        [Theory]
        [InlineData(@"[{last: 'Last', dob: '2020-01-01', ssn: '000-00-000'}]")] // Invalid Ssn format
        [InlineData(@"[{last: '', dob: '2020-01-01', ssn: '000-00-0000'}]")] // Empty last
        [InlineData(@"[{last: '        ', dob: '2020-01-01', ssn: '000-00-0000'}]")] // Whitespace last
        [InlineData(@"[{last: 'Last', first: '', dob: '2020-01-01', ssn: '000-00-000'}]")] // Empty first
        [InlineData(@"[{last: 'Last', first: '       ', dob: '2020-01-01', ssn: '000-00-000'}]")] // Whitespace first
        [InlineData(@"[{last: 'Last', dob: '2020-01-01', ssn: '000000000'}]")] // Invalid Ssn format
        public async void ExpectBadResultFromInvalidData(string query)
        {
            // Arrange
            var api = Construct();
            Mock<HttpRequest> mockRequest = MockRequest(JsonBody(query));
            var logger = Mock.Of<ILogger>();

            // Act
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            var result = response as BadRequestObjectResult;
            Assert.Equal(400, result.StatusCode);

            var errorResponse = result.Value as ApiErrorResponse;
            Assert.Equal(1, (int)errorResponse.Errors.Count);
            Assert.Equal("400", errorResponse.Errors[0].Status);
            Assert.NotEmpty(errorResponse.Errors[0].Title);
            Assert.NotEmpty(errorResponse.Errors[0].Detail);
        }

        // Incomplete data results in BadRequest
        [Theory]
        [InlineData("")]
        [InlineData(@"[{first: 'First'}]")] // Missing Last, Dob, and Ssn
        [InlineData(@"[{last: 'Last'}]")] // Missing Dob and Ssn
        [InlineData(@"[{last: 'Last', dob: '2020-01-01'}]")] // Missing Ssn
        [InlineData(@"[{last: 'Last', dob: '2020-01-1', ssn: '000-00-000'}]")] // Invalid Dob DateTime
        [InlineData(@"[{last: 'Last', dob: '', ssn: '000-00-000'}]")] // Empty Dob DateTime
        [InlineData(@"[{last: 'Last', dob: '2020-01-01', ssn: '000-00-000'}]")] // Missing First
        public async void ExpectBadResultFromIncompleteData(string query)
        {
            // Arrange
            var api = Construct();
            Mock<HttpRequest> mockRequest = MockRequest(JsonBody(query));
            var logger = Mock.Of<ILogger>();

            // Act
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            var result = response as BadRequestObjectResult;
            Assert.Equal(400, result.StatusCode);

            var errorResponse = result.Value as ApiErrorResponse;
            Assert.Equal(1, (int)errorResponse.Errors.Count);
            Assert.Equal("400", errorResponse.Errors[0].Status);
            Assert.NotEmpty(errorResponse.Errors[0].Title);
            Assert.NotEmpty(errorResponse.Errors[0].Detail);
        }

        // Successful API call
        [Fact]
        public async void SuccessfulApiCall()
        {
            // Arrange Mocks
            var logger = Mock.Of<ILogger>();
            var mockRequest = MockRequest(FullRequest().ToJson());
            var mockHandler = MockMessageHandler(new List<HttpResponseMessage>() {
                MockResponse(HttpStatusCode.OK, StateResponse().ToJson())
            });

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
            var mockHandler = MockMessageHandler(new List<HttpResponseMessage>() {
                MockResponse(HttpStatusCode.InternalServerError, "")
            });

            // Arrage Environment
            var uriString = "[\"https://localhost/\"]";
            Environment.SetEnvironmentVariable("StateApiUriStrings", uriString);

            // Act
            var api = ConstructMocked(mockHandler);
            var response = await api.Query(mockRequest.Object, logger);
            var result = response as JsonResult;
            var resBody = result.Value as OrchMatchResponse;
            var error = resBody.Data.Errors[0];

            // Assert
            Assert.Equal(0, (int)resBody.Data.Results.Count);
            Assert.Equal(1, (int)resBody.Data.Errors.Count);
            Assert.Equal(0, error.Index);
            Assert.NotNull(error.Code);
            Assert.NotNull(error.Detail);

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

        // Multiple Queries tests
        // Successful API call
        [Fact]
        public async void SuccessfulApiCallMultipleQueries()
        {
            // Arrange Mocks
            var logger = Mock.Of<ILogger>();
            var mockRequest = MockRequest(FullRequestMultiple().ToJson());
            var mockHandler = MockMessageHandler(new List<HttpResponseMessage>() {
                MockResponse(HttpStatusCode.OK, StateResponse().ToJson()),
                MockResponse(HttpStatusCode.OK, StateResponse().ToJson())
            });

            // Arrage Environment
            var uriString = "[\"https://localhost/\"]";
            Environment.SetEnvironmentVariable("StateApiUriStrings", uriString);

            // Act
            var api = ConstructMocked(mockHandler);
            var response = await api.Query(mockRequest.Object, logger);

            // Assert - top-level data
            var res = response as JsonResult;
            var resBody = res.Value as OrchMatchResponse;
            Assert.Equal(2, (int)resBody.Data.Results.Count);
            Assert.Equal(0, (int)resBody.Data.Errors.Count);

            // Assert results data
            var result = resBody.Data.Results[0];
            Assert.NotEmpty(result.Matches);
            Assert.Equal(0, result.Index);

            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        // Over the max number pf persons in a request
        [Fact]
        public async void OverMaxInRequestReturnsError()
        {
            // Arrange Mocks
            var logger = Mock.Of<ILogger>();
            var mockRequest = MockRequest(OverMaxRequest().ToJson());
            var mockHandler = MockMessageHandler(new List<HttpResponseMessage>() {
                MockResponse(HttpStatusCode.OK, StateResponse().ToJson())
            });

            // Arrage Environment
            var uriString = "[\"https://localhost/\"]";
            Environment.SetEnvironmentVariable("StateApiUriStrings", uriString);

            // Act
            var api = ConstructMocked(mockHandler);
            var response = await api.Query(mockRequest.Object, logger);

            // Assert
            var result = response as BadRequestObjectResult;
            Assert.Equal(400, result.StatusCode);

            var errorResponse = result.Value as ApiErrorResponse;
            Assert.Equal(1, (int)errorResponse.Errors.Count);
            Assert.Equal("400", errorResponse.Errors[0].Status);
            Assert.NotEmpty(errorResponse.Errors[0].Title);
            Assert.NotEmpty(errorResponse.Errors[0].Detail);
        }

        // Multiple persons in request——returns error for one and success for another
        [Fact]
        public async void ItemLevelErrorIsPresent()
        {
            // Arrange Mocks
            var logger = Mock.Of<ILogger>();
            var mockRequest = MockRequest(FullRequestMultiple().ToJson());
            var mockHandler = MockMessageHandler(new List<HttpResponseMessage>() {
                MockResponse(HttpStatusCode.OK, StateResponse().ToJson()),
                MockResponse(HttpStatusCode.InternalServerError, "")
            });

            // Arrage Environment
            var uriString = "[\"https://localhost/\"]";
            Environment.SetEnvironmentVariable("StateApiUriStrings", uriString);

            // Act
            var api = ConstructMocked(mockHandler);
            var response = await api.Query(mockRequest.Object, logger);
            var res = response as JsonResult;
            var resBody = res.Value as OrchMatchResponse;
            var error = resBody.Data.Errors[0];

            // Assert
            Assert.Equal(1, (int)resBody.Data.Results.Count);
            Assert.Equal(1, (int)resBody.Data.Errors.Count);
            Assert.Equal(1, error.Index);
            Assert.NotNull(error.Code);
            Assert.NotNull(error.Detail);
        }

        // Whole thing blows up and returns a top-level error
        [Fact]
        public async void ReturnsInternalServerError()
        {
            // Arrange
            var api = Construct();
            Mock<HttpRequest> mockRequest = MockRequest("foobar");
            var logger = new Mock<ILogger>();

            // Set up first log to throw an exception
            // How to mock LogInformation: https://stackoverflow.com/a/58413842
            logger.SetupSequence(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
            .Throws(new Exception("example message"));

            // Act
            var response = await api.Query(mockRequest.Object, logger.Object);
            var result = response as JsonResult;
            var resBody = result.Value as ApiErrorResponse;
            var error = resBody.Errors[0];

            // Assert
            Assert.Equal(500, result.StatusCode);
            Assert.NotEmpty(resBody.Errors);
            Assert.Equal("500", error.Status);
            Assert.NotNull(error.Title);
            Assert.NotNull(error.Detail);
        }
    }
}
