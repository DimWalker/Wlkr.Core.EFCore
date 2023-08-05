using System;
using System.Collections.Generic;

namespace EFCoreSample.Models
{
    public partial class TestModel
    {
        public int Id { get; set; }
        public string? S { get; set; }
        public bool? B { get; set; }
        public long? L { get; set; }
        public double? F { get; set; }
        public decimal? D { get; set; }
        public Guid? G { get; set; }
        public DateTime? CreateDate { get; set; }
    }
}
