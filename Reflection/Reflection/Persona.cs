using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TostadoPersistentKit;

namespace Reflection
{
    class Persona : Humano
    {
        public string nombre { get; set; }

        public int edad { get; set; }

        public Humano humano { get; set; }

        internal override void map()
        {
            mappings.Add("nombre", "name");
            mappings.Add("edad", "age");
            mappings.Add("humano", "idHumano");
        }
    }
}
