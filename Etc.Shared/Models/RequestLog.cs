using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Etc.Shared.Models
{
    public class RequestLog
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }
        public string? RequestMethod { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestQuery { get; set; }
        public string? RequestHeaders { get; set; }
        public string? RequestPayload { get; set; }
        public string? ResponsePayload { get; set; }
        public int? StatusCode { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime? ResponseTime { get; set; }
        public long? DurationMs { get; set; }
        public string? ClientIp { get; set; }
        public string? UserAgent { get; set; }
    }
}
