using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Piipan.Shared.Authentication;
using Xunit;

namespace Piipan.Match.Orchestrator.Tests
{
    public class LookupTests
    {

        static Api ConstructMockedApi()
        {
            var apiClient = Mock.Of<IAuthorizedApiClient>();
            var lookupStorage = ApiTests.MockLookupStorage();
            var api = new Api(apiClient, lookupStorage.Object);

            return api;
        }

        [Fact]
        public void LookupIdConformsToLength()
        {
            var id = LookupId.Generate();

            Assert.Equal(7, id.Length);
        }

        [Fact]
        public void LookupIdConformsToAlphabet()
        {
            var disallowed = "01AEIOUabcdefghijklmnopqrstuvwxyz";
            var id = LookupId.Generate();

            foreach (char c in disallowed)
            {
                Assert.False(id.Contains(c));
            }
        }

        [Fact]
        public void LookupResponseJson()
        {
            var mq = new RequestPerson
            {
                First = "first",
                Middle = "middle",
                Last = "last",
                Dob = new DateTime(1970, 1, 1),
                Ssn = "000-00-0000"
            };
            var lr = new LookupResponse { Data = mq };

            Assert.Contains("\"first\": \"first\"", lr.ToJson());
            Assert.Contains("\"middle\": \"middle\"", lr.ToJson());
            Assert.Contains("\"last\": \"last\"", lr.ToJson());
            Assert.Contains("\"dob\": \"1970-01-01\"", lr.ToJson());
            Assert.Contains("\"ssn\": \"000-00-0000\"", lr.ToJson());
        }

        [Fact]
        public async void SuccessfulApiCall()
        {
            // Arrange
            var api = ConstructMockedApi();
            var mockRequest = new Mock<HttpRequest>();
            var headers = new HeaderDictionary(new Dictionary<String, StringValues>
            {
                { "X-username", "foobar"}
            }) as IHeaderDictionary;
            mockRequest.Setup(x => x.Headers).Returns(headers);

            // Act
            var result = await api.LookupIds(mockRequest.Object, "ABC1234", Mock.Of<ILogger>());
            var jsonResult = result as JsonResult;
            var lookupResponse = jsonResult.Value as LookupResponse;

            // Assert
            Assert.IsType<JsonResult>(result);
            Assert.IsType<LookupResponse>(jsonResult.Value);
            Assert.IsType<RequestPerson>(lookupResponse.Data);
        }
    }
}
