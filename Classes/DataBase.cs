using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using System.Windows.Forms;
using System.IO;

namespace TostadoPersistentKit
{
    public class DataBase
    {
        #region Singleton
        private static volatile DataBase instancia = null;

        public static DataBase Instance
        {
            get
            { return newInstance(); }
        }

        internal static DataBase newInstance()
        {
            if (instancia != null) { }
            else
            {
                instancia = new DataBase();
            }
            return instancia;
        }

        #endregion
        //Para obtener instancia escribir Database.Instance

        #region Propiedades

        private DateTime fechaDatabase;

        private string instanciaSql = "";

        private string db = "";

        private string username = "";

        private string password = "";

        private string datosConexion;

        #endregion

        private DataBase()
        {
            cargarDatos();
        }

        private SqlConnection abrirConexion()
        {
            SqlConnection conexion = new SqlConnection();
            conexion.ConnectionString = datosConexion;
            conexion.Open();
            return conexion;
        }

        private SqlDataReader getDataReader(string consulta,char tipoConsulta,List<SqlParameter> parametros,SqlConnection conexion)
        {
            SqlCommand comando = new SqlCommand();
            comando.Connection = conexion;
            comando.CommandText = consulta;

            switch (tipoConsulta)
            {
                case 'T':
                    comando.CommandType = CommandType.Text;
                    break;
                /*case 'D':
                    comando.CommandType = CommandType.TableDirect;
                    break;*///Lo comento porque por ahora no se cuando se usaria
                case 'P':
                    comando.CommandType = CommandType.StoredProcedure;
                    break;
            }

            if (parametros!=null)
            {
                foreach (SqlParameter parametro in parametros)
                {
                    comando.Parameters.Add(parametro);
                }
            }

            SqlDataReader reader= comando.ExecuteReader();

            return reader;
        }

        public void agregarParametro(List<SqlParameter> lista, string parametro, object valor)
        {
            lista.Add(new SqlParameter(parametro, valor));
        }

        private List<Dictionary<string, object>> executeQueryableCommand(string command, List<SqlParameter> parameters,char commandType)
        {
            SqlConnection connection = abrirConexion();

            List<Dictionary<string, object>> rows = adapterDiccionario(getDataReader(command, commandType, parameters, connection));

            connection.Close();

            return rows;
        }

        public object ejecutarStoredConRetorno(string storedProcedure, List<SqlParameter> parametros,string nombreVariableRetorno, object valorVariableRetorno)
        {
            SqlConnection connection = abrirConexion();

            SqlCommand cmd = new SqlCommand(storedProcedure, connection);
            cmd.CommandType = CommandType.StoredProcedure;
            foreach (SqlParameter parametro in parametros)
            {
                cmd.Parameters.Add(parametro);
            }
            SqlParameter retornoParametro = new SqlParameter(nombreVariableRetorno, valorVariableRetorno);
            retornoParametro.Direction = ParameterDirection.Output;
            cmd.Parameters.Add(retornoParametro);
            cmd.ExecuteNonQuery();
            object retorno = cmd.Parameters[nombreVariableRetorno].Value;
            connection.Close();

            return retorno;
        }

        public List<Dictionary<string, object>> ejecutarStoredProcedure(string storedProcedure,List<SqlParameter> parametros)
        {
            return executeQueryableCommand(storedProcedure, parametros, 'P');
        }

        public List<Dictionary<string, object>> ejecutarConsulta(string consulta, List<SqlParameter> parametros)
        {
            return executeQueryableCommand(consulta, parametros, 'T');
        }

        public List<Dictionary<string, object>> ejecutarConsulta(string consulta)
        {
            return ejecutarConsulta(consulta, new List<SqlParameter>());
        }

        public List<Dictionary<string, object>> adapterDiccionario(SqlDataReader reader)
        {
            List<Dictionary<string, object>> lista = new List<Dictionary<string, object>>();

            if (reader.HasRows)
            {
                bool notEnded = reader.Read();

                string columna = "";

                while (notEnded)
                {
                    Dictionary < string, object> filaDiccionario=new Dictionary<string, object>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    { 

                    columna = reader.GetName(i);

                        if (!filaDiccionario.ContainsKey(columna))
                        {
                            filaDiccionario.Add(columna, reader[columna]);
                        }
                    }

                    lista.Add(filaDiccionario);

                    notEnded = reader.Read();
                    }
                }

                return lista;
            }

        internal bool executeScript(string route)
        {
            string script = File.ReadAllText(route);

            bool salioTodoBien = true;

            List<string> operations = script.Split(new[] { "GO", "Go", "go", "gO" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var operation in operations)
            {
                try
                {
                    ejecutarConsulta(operation);
                }
                catch (Exception)
                {
                    salioTodoBien = false;
                    //Escondemos los errores cmo unos campeones
                }
            }

            return salioTodoBien;
        }

        #region Accesors e Inicializacion

        private void cargarDatos()
        {
            System.IO.StreamReader file = new System.IO.StreamReader("configuracion_sistema.txt");
            string line;
            List<string> listaParser = new List<string>();

            while ((line = file.ReadLine()) != null)
            {
                if (line != "")
                {
                    listaParser.Add(line);
                }
            }

            cargarVariables(listaParser);

            datosConexion = @"Data Source=localhost\" + instanciaSql + ";"
                           + "Initial Catalog=" + db + "; Integrated Security=false;"
                           + "UID=" + username + ";PWD=" + password + ";";

            string[] listaFecha = listaParser[0].Split('=')[1].Trim().Split('/');

            fechaDatabase = new DateTime(Convert.ToInt16(listaFecha[2]), Convert.ToInt16(listaFecha[1]), Convert.ToInt16(listaFecha[0]));
        }

        private void cargarVariables(List<string> listaParser)
        {
            instanciaSql = listaParser[1].Split('=')[1].Trim();
            db = listaParser[2].Split('=')[1].Trim();
            username = listaParser[3].Split('=')[1].Trim();
            password = listaParser[4].Split('=')[1].Trim();
        }

        internal DateTime getDate()
        {
            return fechaDatabase;
        }

        internal string getSqlInstance()
        {
            return instanciaSql;
        }

        internal string getDb()
        {
            return db;
        }

        internal string getUsername()
        {
            return username;
        }

        internal string getPassword()
        {
            return username;
        }

        #endregion
    }

}
