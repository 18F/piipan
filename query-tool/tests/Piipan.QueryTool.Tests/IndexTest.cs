using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Piipan.QueryTool.Pages;
using Piipan.Shared.Authentication;
using Xunit;

namespace Piipan.QueryTool.Tests
{
    public class IndexPageTests
    {
        public IndexPageTests()
        {
            Environment.SetEnvironmentVariable("OrchApiUri", "https://localhost/");
        }

        public static IAuthorizedApiClient clientMock(HttpStatusCode statusCode, string returnValue)
        {
            var clientMock = new Mock<IAuthorizedApiClient>();
            clientMock
                .Setup(c => c.PostAsync(
                    It.Is<Uri>(u => u.ToString().Contains("/query")),
                    It.IsAny<StringContent>()
                ))
                .Returns(Task.FromResult(new HttpResponseMessage()
                {
                    StatusCode = statusCode,
                    Content = new StringContent(returnValue)
                }));

            return clientMock.Object;
        }

        [Fact]
        public void TestBeforeOnGet()
        {
            // arrange

            var mockApiClient = Mock.Of<IAuthorizedApiClient>();
            var pageModel = new IndexModel(
                new NullLogger<IndexModel>(),
                mockApiClient
                );
            // act
            // assert
            Assert.Equal("", pageModel.Title);
        }
        [Fact]
        public void TestAfterOnGet()
        {
            // arrange
            var mockApiClient = Mock.Of<IAuthorizedApiClient>();
            var pageModel = new IndexModel(new NullLogger<IndexModel>(), mockApiClient);

            // act
            pageModel.OnGet();

            // assert
            Assert.Equal("NAC Query Tool", pageModel.Title);
        }

        [Fact]
        public async void MatchSetsResults()
        {
            // arrange
            var returnValue = @"{
                ""data"": {
                    ""results"": [
                        {
                            ""lookup_id"": ""BBB2222"",
                            ""matches"": [{
                                ""first"": ""Theodore"",
                                ""middle"": ""Carri"",
                                ""last"": ""Farrington"",
                                ""ssn"": ""000-00-0000"",
                                ""dob"": ""2021-01-01"",
                                ""state"": ""ea"",
                                ""state_abbr"": ""ea""
                            }]
                        }
                    ],
                    ""errors"": []
                }
            }";
            var requestPii = new PiiRecord
            {
                FirstName = "Theodore",
                LastName = "Farrington",
                SocialSecurityNum = "000-00-0000",
                DateOfBirth = new DateTime(2021, 1, 1)
            };
            var mockClient = clientMock(HttpStatusCode.OK, returnValue);
            var pageModel = new IndexModel(new NullLogger<IndexModel>(), mockClient);
            pageModel.Query = requestPii;

            // act
            await pageModel.OnPostAsync();

            // assert
            Assert.IsType<MatchResponse>(pageModel.QueryResult);
            Assert.NotNull(pageModel.QueryResult);
            Assert.NotNull(pageModel.QueryResult.Data.Results[0].Matches);
            Assert.False(pageModel.NoResults);
        }

        [Fact]
        public async void MatchNoResults()
        {
            // arrange
            var returnValue = @"{
                ""data"": {
                    ""results"": [
                        {
                            ""lookup_id"": null,
                            ""matches"": []
                        }
                    ],
                    ""errors"": []
                }
            }";
            var requestPii = new PiiRecord
            {
                FirstName = "Theodore",
                LastName = "Farrington",
                SocialSecurityNum = "000-00-0000",
                DateOfBirth = new DateTime(2021, 1, 1)
            };
            var mockClient = clientMock(HttpStatusCode.OK, returnValue);
            var pageModel = new IndexModel(new NullLogger<IndexModel>(), mockClient);
            pageModel.Query = requestPii;

            // act
            await pageModel.OnPostAsync();

            // assert
            Assert.IsType<MatchResponse>(pageModel.QueryResult);
            Assert.Null(pageModel.QueryResult.Data.Results[0].LookupId);
            Assert.Empty(pageModel.QueryResult.Data.Results[0].Matches);
            Assert.True(pageModel.NoResults);
        }

        [Fact]
        public async void MatchCapturesApiError()
        {
            // arrange
            var requestPii = new PiiRecord
            {
                FirstName = "Theodore",
                LastName = "Farrington",
                SocialSecurityNum = "000-00-0000",
                DateOfBirth = new DateTime(2021, 1, 1)
            };
            var mockClient = clientMock(HttpStatusCode.BadRequest, "");
            var pageModel = new IndexModel(new NullLogger<IndexModel>(), mockClient);
            pageModel.Query = requestPii;

            // act
            await pageModel.OnPostAsync();

            // assert
            Assert.NotNull(pageModel.RequestError);
        }
    }
}
