using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TostadoPersistentKit;

namespace Reflection
{
    public class Persona : Humano
    {
        public string nombre { get; set; }

        public int dni { get; set; }

        public int edad { get; set; }

        public Humano humano { get; set; }

        internal override void map()
        {
            mappings.Add("nombre", "nombre");
            mappings.Add("edad", "edad");
            mappings.Add("dni", "dni");
        }
    }
}
