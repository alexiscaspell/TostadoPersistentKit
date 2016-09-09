using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TostadoPersistentKit {

    class Sistema
    {
        #region Singleton
        private static volatile Sistema instancia = null;

        public static Sistema Instance
        {
            get
            { return newInstance(); }
        }

        internal static Sistema newInstance()
        {
            if (instancia != null) { }
            else
            {
                instancia = new Sistema();
            }
            return instancia;
        }


        #endregion

        private string datosConexion; /*= @"Data Source=localhost\SQLSERVER2012;"
                        + "Initial Catalog=GD1C2016;Integrated Security=false;"
                        + "UID=gd;PWD=gd2016;";*/

        private DateTime fechaSistema;

        internal string getDBConfigurations()
        {
            return datosConexion;
        }

        private Sistema()
        {
            cargarDatosDeSistema();
        }

        private void cargarDatosDeSistema()
        {
            System.IO.StreamReader file = new System.IO.StreamReader("configuracion_sistema.txt");
            string line;
            List<string> listaParser = new List<string>();

            while ((line = file.ReadLine()) != null)
            {
                if (line!="")
                {
                    listaParser.Add(line);
                }
            }

            datosConexion = @"Data Source=localhost\" + listaParser[1].Split('=')[1].Trim() + ";"
                           + "Initial Catalog=" + listaParser[2].Split('=')[1].Trim() + "; Integrated Security=false;"
                           + "UID=" + listaParser[3].Split('=')[1].Trim() + ";PWD=" + listaParser[4].Split('=')[1].Trim() + ";";

            string[] listaFecha = listaParser[0].Split('=')[1].Trim().Split('/');

            fechaSistema = new DateTime(Convert.ToInt16(listaFecha[2]), Convert.ToInt16(listaFecha[1]), Convert.ToInt16(listaFecha[0]));
        }

        internal DateTime getDate()
        {
            return fechaSistema;
        }
    }
}
