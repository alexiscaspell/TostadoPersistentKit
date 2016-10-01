using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TostadoPersistentKit
{
    public abstract class MappingAttribute:Attribute
    {
        public String name { get; set; }

        public MappingAttribute()
        {
            name = "";
        }
    }
}
