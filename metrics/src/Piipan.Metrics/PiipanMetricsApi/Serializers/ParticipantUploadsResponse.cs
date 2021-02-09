using System;
using System.Collections.Generic;
using Piipan.Metrics.Models;

namespace Piipan.Metrics.Api
{
    namespace Serializers
    {
        public class ParticipantUploadsResponse : Response
        {
            public Meta meta;
            public List<ParticipantUpload> data;
            public ParticipantUploadsResponse(
                List<ParticipantUpload> responseData,
                int total,
                int limit)
            {
                data = responseData;
                meta = new Meta();
                meta.total = total;
                meta.limit = limit;
                meta.offset = 0;
            }
        }
    }
}
