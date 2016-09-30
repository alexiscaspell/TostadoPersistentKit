using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using System.Windows.Forms;

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

        private string datosConexion = Sistema.Instance.getDBConfigurations();

        private DataBase()
        {
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
    }

    }
