using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TostadoPersistentKit;

namespace Reflection
{
    class PersonaRepository:Repository
    {
        public Persona traerPersonaCualquiera()
        {
            Dictionary<string, object> diccionario = new Dictionary<string, object>();

            diccionario.Add("name", "Jesus");
            diccionario.Add("age", 21);

            return (Persona)unSerialize(diccionario);
        }

        internal override void setModelClassType()
        {
            modelClassType = typeof(Persona);
        }
    }
}
