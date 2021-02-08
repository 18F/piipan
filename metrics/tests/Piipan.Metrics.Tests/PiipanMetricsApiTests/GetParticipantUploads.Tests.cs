using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Piipan.Metrics.Api;
using Piipan.Metrics.Models;

namespace Piipan.Metrics.Tests
{
    namespace Piipan.Metrics.Api.Tests
    {
        public class GetParticipantUploadsTests
        {
            static HttpRequest EventMock()
            {
                var context = new DefaultHttpContext();
                return context.Request;
            }

            static Mock<ILogger> Logger()
            {
                return new Mock<ILogger>();
            }

            // returns a 200
            [Fact]
            public async void ReadSuccess() {
                var logger = Logger();
                var factory = new Mock<DbProviderFactory>() { DefaultValue = DefaultValue.Mock };
                var cmd = new Mock<DbCommand>() { DefaultValue = DefaultValue.Mock };
                factory.Setup(f => f.CreateCommand()).Returns(cmd.Object);

                var results = await GetParticipantUploads.Read(factory.Object, logger.Object);

                Assert.Equal(new List<ParticipantUpload>(), results);
            }
        }
    }
}
