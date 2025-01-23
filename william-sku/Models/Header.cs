using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace william_sku.Models
{
    public class Header
    {
        public bool IsSelected { get; set; }
        public string? Name { get; set; }
        public string? Display { get; set; }
        public bool Required { get; set; } = false;
        public bool Range { get; set; } = false;

        public int OrderIndex { get; set; }
    }
}
