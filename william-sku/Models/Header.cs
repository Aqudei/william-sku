using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace william_sku.Models
{
    internal class Header
    {
        public string Name { get; set; }
        public string Display { get; set; }
        public bool Required { get; set; } = false;
        public bool Range { get; set; } = false;
    }
}
