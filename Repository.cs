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

        //internal Type modelClassType;

        //Esto setea la clase que el repositorio va a tener como modelo para mapear
        internal abstract Type getModelClassType();


        internal object executeStored(String storedProcedure,List<SqlParameter> parameters)
        {
            List<Dictionary<string,object>> dictionaryList = DataBase.Instance.ejecutarStoredProcedure(storedProcedure, parameters);

            return returnValue(dictionaryList);
        }

        internal object executeQuery(String query, List<SqlParameter> parameters)
        {
            List<Dictionary<string, object>> dictionaryList = DataBase.Instance.ejecutarConsulta(query, parameters);

            return returnValue(dictionaryList);
        }

        private object returnValue(List<Dictionary<string,object>> dictionaryList)
        {
            if (autoMapping)
            {
                List<object> mappedList = new List<object>();

                dictionaryList.ForEach(dictionary => mappedList.Add(unSerialize(dictionary)));

                return mappedList;
            }
            else
            {
                return dictionaryList;
            }
        }

        internal object unSerialize(Dictionary<string, object> dictionary)
        {
            return unSerialize(dictionary, getModelClassType());
        }

        private object unSerialize(Dictionary<string, object> dictionary,Type modelClassType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(modelClassType);

            Dictionary<string, object> dictionaryAux = copyDictionary(dictionary);

            foreach (String dataName in dictionary.Keys)
            {
                String propertyName = objeto.getMapFromVal(dataName);
                String keyToRemove = dataName;

                if (propertyName != "")
                {
                    Type propertyType = objeto.GetType().GetProperty(propertyName).PropertyType;//.GetType();

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);

                    if (isSerializable)
                    {
                        Serializable propertyInstance = (Serializable)Activator.CreateInstance(propertyType);
                        keyToRemove = propertyInstance.getMapFromKey(propertyInstance.getIdPropertyName());

                        object dataValue = dictionaryAux[dataName];
                        dictionaryAux.Remove(dataName);
                        dictionaryAux.Add(keyToRemove, dataValue);

                        objeto.GetType().GetProperty(propertyName).SetValue(objeto, unSerialize(dictionaryAux, propertyType));
                    }
                    else
                    {
                        if (dictionary[dataName].ToString()!="")//Esto esta hardcodeado para que no setee cosas en null
                        {
                            object dataValue = getCastedValue(dictionary[dataName], objeto.GetType().GetProperty(propertyName).PropertyType);

                            objeto.GetType().GetProperty(propertyName).SetValue(objeto, dataValue);
                        }
                    }
                    dictionaryAux.Remove(keyToRemove);
                }
            }

            return objeto;
        }

        private object getCastedValue(object value, Type expectedType)
        {
            /*bool isTypeChar = typeof(char).IsAssignableFrom(expectedType);

            if (isTypeChar)
            {
                return value.ToString()[0];
            }*/

            return Convert.ChangeType(value, expectedType);
        }

        private Dictionary<string, object> copyDictionary(Dictionary<string, object> dictionary)
        {
            Dictionary<string, object> copyDictionary = new Dictionary<string, object>();

            foreach (KeyValuePair<string,object> keyValuePair in dictionary)
            {
                copyDictionary.Add(keyValuePair.Key, keyValuePair.Value);
            }

            return copyDictionary;
        }

        private List<String> listSerializableProperties(object objeto)
        {
            List<String> serializableProperties = new List<string>();

            foreach (MemberInfo info in objeto.GetType().GetMembers())
            {
                if (info.MemberType==MemberTypes.Property)
                {
                    Type propertyType = ((PropertyInfo)info).PropertyType;

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);
                    if (isSerializable)
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
            insert(objeto,objeto.getIdPropertyName(), objeto.getTableName());
        }

        private void insert(Serializable objeto, String primaryKeyPropertyName, String tableName)
        {
            String insertQuery = "insert into " + tableName + "(";

            String valuesString = " values (";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key != primaryKeyPropertyName || objeto.getPrimaryKeyType() == Serializable.PrimaryKeyType.NATURAL)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    insertQuery += dataName + ",";

                    valuesString += "@" + dataName + ",";

                    bool isSerializableProperty = typeof(Serializable).IsAssignableFrom(keyValuePair.Value.GetType());

                    Serializable serializableProperty = isSerializableProperty ? (Serializable)keyValuePair.Value : null;

                    object parametro = isSerializableProperty ? serializableProperty.GetType().
                                        GetProperty(serializableProperty.getIdPropertyName()).
                                        GetValue(serializableProperty) : keyValuePair.Value;

                    DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                }
            }

            insertQuery = insertQuery.Remove(insertQuery.Length - 1);
            valuesString = valuesString.Remove(valuesString.Length - 1);
            insertQuery += ")" + valuesString + ")";

            DataBase.Instance.ejecutarConsulta(insertQuery, parametros);
        }

        internal void delete(Serializable objeto)
        {
            delete(objeto, objeto.getIdPropertyName(), objeto.getTableName());
        }

        private void delete(Serializable objeto,String primaryKeyPropertyName,String tableName)
        {
            String deleteQuery = "delete from " + tableName + " where " 
                                + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                                + objeto.GetType().GetProperty(primaryKeyPropertyName).GetValue(objeto).ToString();

            DataBase.Instance.ejecutarConsulta(deleteQuery);
        }

        //Se supone que lo que se espera en el where de la consulta es id.toString()
        internal object selectById(object id)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(getModelClassType());

            List<object> result = selectByProperty(objeto.getIdPropertyName(), id);

            return (result.Count > 0) ? result[0] : null;
        }

        internal List<object> selectByProperty(string propertyName,object propertyValue)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(getModelClassType());

            List<SqlParameter> parameters = new List<SqlParameter>();

            bool isCharObject = typeof(string).IsAssignableFrom(propertyValue.GetType()) || typeof(char).IsAssignableFrom(propertyValue.GetType());

            string expected = isCharObject ? "'" + propertyValue.ToString() + "'" : propertyValue.ToString();

            string selectQuery = "select * from " + objeto.getTableName() + " where " +
                                objeto.getMapFromKey(propertyName) + "=" + expected;

            return executeAutoMappedSelect(selectQuery, parameters);
        }

        private List<object> executeAutoMappedSelect(String selectQuery,List<SqlParameter> parameters)
        {
            bool actualAutoMappingVal = autoMapping;//guardo el valor actual de autoMapping

            autoMapping = true;

            List<object> result = (List<object>)executeQuery(selectQuery, parameters);

            autoMapping = actualAutoMappingVal;

            return result;
        }

        internal List<object> selectAll()
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(getModelClassType());

            return selectAll(objeto.getTableName());
        }

        private List<object> selectAll(String tableName)
        {
            List<SqlParameter> parameters = new List<SqlParameter>();

            String selectQuery = "select * from " + tableName;

            return executeAutoMappedSelect(selectQuery, parameters);
        }

        internal void update(Serializable objeto)
        {
            update(objeto, objeto.getIdPropertyName(), objeto.getTableName(),false);
        }

        //No setea valores null
        //Si se modifico la pk aunque sea natural no la updatea (usea ignora la pk)
        private void update(Serializable objeto, String primaryKeyPropertyName, String tableName, bool cascadeMode)
        {
            String updateQuery = "update " + tableName + " set ";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key != primaryKeyPropertyName)// || objeto.primaryKetyType == Serializable.PrimaryKeyType.NATURAL)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    updateQuery += dataName + "=@" + dataName + ",";

                    bool isSerializableProperty = typeof(Serializable).IsAssignableFrom(keyValuePair.Value.GetType());

                    Serializable serializableProperty = isSerializableProperty ? (Serializable)keyValuePair.Value : null;

                    object parametro = isSerializableProperty ? serializableProperty.GetType().
                                        GetProperty(serializableProperty.getIdPropertyName()).
                                        GetValue(serializableProperty) : keyValuePair.Value;

                    DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                }
            }

            updateQuery = updateQuery.Remove(updateQuery.Length - 1);

            updateQuery += " where " + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                        + objeto.GetType().GetProperty(primaryKeyPropertyName).GetValue(objeto);

            if (cascadeMode)
            {
                List<String> serializablePropertyNames = listSerializableProperties(objeto);

                foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
                {
                    //Aca supongo que no puede ser null
                    if (serializablePropertyNames.Contains(keyValuePair.Key))
                    {
                        Serializable serializableProperty = (Serializable)keyValuePair.Value;

                        update(serializableProperty, serializableProperty.getIdPropertyName(), serializableProperty.getTableName(), cascadeMode);
                    }
                }
            }

            DataBase.Instance.ejecutarConsulta(updateQuery, parametros);
        }

        internal void updateCascade(Serializable objeto)
        {
            update(objeto, objeto.getIdPropertyName(), objeto.getTableName(),true);
        }
    }
}
