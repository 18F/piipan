using System;
using System.Data.Common;
using Xunit;
using Piipan.Metrics.Models;

namespace Piipan.Metrics.Tests
{
    namespace Piipan.Metrics.Models.Tests
    {
        public class ParticipantUploadTests
        {
            [Fact]
            public static void CreateSuccess()
            {
                var result = new ParticipantUpload();
                result.state = "foobar";
                result.uploaded_at = new DateTime();

                Assert.Equal("foobar", result.state);
                Assert.Equal(typeof(DateTime), result.uploaded_at.GetType());
            }
        }

    }
}
