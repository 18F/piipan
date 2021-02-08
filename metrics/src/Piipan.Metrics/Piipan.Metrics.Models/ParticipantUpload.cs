using System;

namespace Piipan.Metrics.Models
{
    /// <summary>
    /// Data Mapper for participant_uploads table in metrics database
    /// </summary>
    public class ParticipantUpload
    {
        public string state { get; set; }
        public DateTime uploaded_at { get; set; }
    }
}
