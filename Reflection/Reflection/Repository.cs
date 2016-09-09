using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;

namespace TostadoPersistentKit
{
    abstract class Repository
    {
        //Esto me dice si se va a mapear sola la clase o a mano, se puede cambiar en cualquier momento
        internal Boolean autoMapping = true;

        internal Type modelClassType;

        //Esto setea la clase que el repositorio va a tener como modelo para mapear
        internal abstract void setModelClassType();

        public Repository()
        {
            setModelClassType();
        }

        internal object executeStored(String storedProcedure,List<SqlParameter> parameters)
        {
            List<Dictionary<string,object>> dictionaryList = DataBase.Instance.ejecutarStoredProcedure(storedProcedure, parameters);

            return returnValue(dictionaryList);
        }

        internal object executeQuery(String query, List<SqlParameter> parameters)
        {
            List<Dictionary<string, object>> dictionaryList = DataBase.Instance.ejecutarStoredProcedure(query, parameters);

            return returnValue(dictionaryList);
        }

        private object returnValue(List<Dictionary<string,object>> dictionaryList)
        {
            if (autoMapping)
            {
                List<Serializable> mappedList = new List<Serializable>();

                dictionaryList.ForEach(dictionary => mappedList.Add(unSerialize(dictionary)));

                return mappedList;
            }
            else
            {
                return dictionaryList;
            }
        }

        internal Serializable unSerialize(Dictionary<string, object> dictionary)
        {
            return unSerialize(dictionary, modelClassType);
        }

        private Serializable unSerialize(Dictionary<string, object> dictionary,Type modelClassType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(modelClassType);

            foreach (String dataName in dictionary.Keys)
            {
                String propertyName = objeto.getMapFromVal(dataName);

                if (propertyName != "")
                {
                    Type propertyType = objeto.GetType().GetProperty(propertyName).PropertyType;//.GetType();

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);

                    if (isSerializable)
                    {
                        objeto.GetType().GetProperty(propertyName).SetValue(objeto, unSerialize(dictionary, propertyType));
                    }
                    else
                    {
                        if (dictionary[dataName].ToString()!="")//Esto esta hardcodeado para que no setee cosas en null
                        {
                            objeto.GetType().GetProperty(propertyName).SetValue(objeto, dictionary[dataName]);
                        }
                    }
                }
            }

            return objeto;
        }

        private List<String> listSerializableProperties(object objeto)
        {
            List<String> serializableProperties = new List<string>();

            foreach (MemberInfo info in objeto.GetType().GetMembers())
            {
                if (info.MemberType==MemberTypes.Property)
                {
                    if (typeof(Serializable).IsAssignableFrom(((PropertyInfo)info).GetType()))
                    {
                        serializableProperties.Add(((PropertyInfo)info).Name);
                    }
                }
            }

            return serializableProperties;
        }

        private Dictionary<string,object> getPropertyValues(Serializable objeto)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            foreach (MemberInfo info in objeto.GetType().GetMembers())
            {
                if (info.MemberType == MemberTypes.Property)
                {
                    string propertyName = ((PropertyInfo)info).Name;

                    object propertyValue = ((PropertyInfo)info).GetValue(objeto);

                    if (propertyValue!=null)
                    {
                        dictionary.Add(propertyName, propertyValue);
                    }
                }
            }

            return dictionary;
        }

        internal void insert(Serializable objeto)
        {
            insert(objeto,objeto.idProperty, objeto.tableName);
        }

        private void insert(Serializable objeto, String primaryKeyPropertyName, String tableName)
        {
            String insertQuery = "insert into " + tableName + "(";

            String valuesString = " values (";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key != primaryKeyPropertyName)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    insertQuery += dataName + ",";

                    valuesString += "@" + dataName + ",";

                    DataBase.Instance.agregarParametro(parametros, "@" + dataName, keyValuePair.Value);
                }
            }

            insertQuery = insertQuery.Remove(insertQuery.Length - 1);
            valuesString = valuesString.Remove(valuesString.Length - 1);
            insertQuery += ")" + valuesString + ")";

            DataBase.Instance.ejecutarConsulta(insertQuery, parametros);
        }

        internal void insertCascade(Serializable objeto)
        {
            insertCascade(objeto, objeto.idProperty, objeto.tableName);
        }

        private void insertCascade(Serializable objeto,String primaryKeyPropertyName,String tableName)
        {
            String insertQuery = "insert into " + tableName + "(";

            String valuesString = " values (";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key != primaryKeyPropertyName)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    insertQuery += dataName + ",";

                    valuesString += "@" + dataName + ",";

                    DataBase.Instance.agregarParametro(parametros, "@" + dataName, keyValuePair.Value);
                }
            }

            insertQuery = insertQuery.Remove(insertQuery.Length - 1);
            valuesString = valuesString.Remove(valuesString.Length - 1);
            insertQuery += ")" + valuesString + ")";

            DataBase.Instance.ejecutarConsulta(insertQuery, parametros);

            List<String> serializablePropertyNames = listSerializableProperties(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                //Aca supongo que no puede ser null
                if (serializablePropertyNames.Contains(keyValuePair.Key))
                {
                    Serializable serializableProperty = (Serializable)keyValuePair.Value;

                    insert(serializableProperty, serializableProperty.idProperty, serializableProperty.tableName);
                }
            }
        }

        internal void update(Serializable objeto)
        {
            update(objeto, objeto.idProperty, objeto.tableName);
        }

        //No setea valores null
        //Esto supone una pk subrogada
        private void update(Serializable objeto, String primaryKeyPropertyName, String tableName)
        {
            String updateQuery = "update " + tableName + " set ";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key!=primaryKeyPropertyName)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    updateQuery += dataName + "=@" + dataName + ",";

                    DataBase.Instance.agregarParametro(parametros, "@" + dataName, keyValuePair.Value);
                }
            }

            updateQuery = updateQuery.Remove(updateQuery.Length - 1);

            updateQuery += " where " + objeto.getMapFromKey(primaryKeyPropertyName) + "=" 
                        + objeto.GetType().GetProperty(primaryKeyPropertyName).GetValue(objeto);

            DataBase.Instance.ejecutarConsulta(updateQuery, parametros);
        }

        internal void delete(Serializable objeto)
        {
            delete(objeto, objeto.idProperty, objeto.tableName);
        }

        private void delete(Serializable objeto,String primaryKeyPropertyName,String tableName)
        {
            String deleteQuery = "delete from " + tableName + " where " 
                                + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                                + objeto.GetType().GetProperty(primaryKeyPropertyName).GetValue(objeto).ToString();

            DataBase.Instance.ejecutarConsulta(deleteQuery);
        }

        internal List<Serializable> selectAll()
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(modelClassType);

            return selectAll(objeto.tableName);
        }

        private List<Serializable> selectAll(String tableName)
        {
            List<Dictionary<string, object>> tabla = DataBase.Instance.ejecutarConsulta("select * from " + tableName);

            List<Serializable> resultList = new List<Serializable>();

            foreach (Dictionary<string,object> fila in tabla)
            {
                resultList.Add(unSerialize(fila, modelClassType));
            }

            return resultList;
        }

    }
}
