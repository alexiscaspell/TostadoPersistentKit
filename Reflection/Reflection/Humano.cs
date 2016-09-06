using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TostadoPersistentKit;

namespace Reflection
{
    class Humano : Serializable
    {
        public int dni { get; set; }

        public String nombreHumano { get; set; }

        internal override void map()
        {
            mappings.Add("dni", "idHumano");
            mappings.Add("nombreHumano", "humanName");
        }
    }
}
