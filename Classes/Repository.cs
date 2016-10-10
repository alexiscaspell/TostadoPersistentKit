using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;

namespace TostadoPersistentKit
{
    public abstract class Repository
    {
        //Esto me dice si se va a mapear sola la clase o a mano, se puede cambiar en cualquier momento
        internal Boolean autoMapping = true;

        /// <summary>
        /// retorna la clase que el repositorio va a tener como modelo para mapear
        /// </summary>
        /// <returns></returns>
        internal abstract Type getModelClassType();

        private List<string> getProcedureParameterNames(String storedProcedure)
        {
            List<string> parameterNames = new List<string>();

            string query = "SELECT * FROM INFORMATION_SCHEMA.PARAMETERS " +
                            "WHERE SPECIFIC_NAME = " +"'"+ storedProcedure +"'"+
                            " ORDER BY SPECIFIC_NAME, ORDINAL_POSITION";

            foreach (Dictionary<string, object> item in DataBase.Instance.ejecutarConsulta(query))
            {
                parameterNames.Add(item["PARAMETER_NAME"].ToString().Split('@')[1]);
            }

            return parameterNames;
        }

        internal object executeStored(String storedProcedure, Serializable objeto)
        {
            return executeStored(storedProcedure, objeto, new List<SqlParameter>());
        }

        internal object executeStored(String storedProcedure,Serializable objeto,List<SqlParameter> extraParameters)
        {
            List<string> parameterNames = getProcedureParameterNames(storedProcedure);

            List<SqlParameter> parameters = new List<SqlParameter>();

            extraParameters.ForEach(o => parameterNames.Remove(o.ParameterName));

            extraParameters.ForEach(o => parameters.Add(o));

            foreach (string parameterName in parameterNames)
            {
                DataBase.Instance.agregarParametro(parameters, parameterName,
                                objeto.getDataValue(parameterName));
            }

            return executeStored(storedProcedure, parameters);
        }

        internal object executeStored(String storedProcedure,List<SqlParameter> parameters)
        {
            return executeStored(storedProcedure,parameters,getModelClassType());
        }

        private object executeStored(String storedProcedure, List<SqlParameter> parameters, Type modelClassType)
        {
            List<Dictionary<string, object>> dictionaryList = DataBase.Instance.ejecutarStoredProcedure(storedProcedure, parameters);

            return returnValue(dictionaryList, modelClassType);
        }

        internal object executeQuery(String query, List<SqlParameter> parameters)
        {
            return executeQuery(query, parameters, getModelClassType());
        }

        private object executeQuery(String query, List<SqlParameter> parameters,Type modelClassType)
        {
            List<Dictionary<string, object>> dictionaryList = DataBase.Instance.ejecutarConsulta(query, parameters);

            return returnValue(dictionaryList,modelClassType);
        }

        private void completeSerializableObject(Serializable incompleteObject)
        {
            foreach (KeyValuePair<string,object> item in getPropertyValues(incompleteObject))
            {
                if (incompleteObject.getFetchType(item.Key)==Serializable.FetchType.EAGER)
                {
                    if (incompleteObject.isOneToManyProperty(item.Key))
                    {
                        completeOneToManyProperty(incompleteObject, item.Key);
                    }

                    if (typeof(Serializable).IsAssignableFrom(item.Value.GetType()))
                    {
                        Serializable serializableProperty = (Serializable)item.Value;

                        Type propertyType = serializableProperty.GetType();

                        object propertyId = propertyType.GetProperty(serializableProperty.
                                                    getIdPropertyName()).GetValue(serializableProperty);


                        serializableProperty = (Serializable)selectById(propertyId, propertyType);

                        incompleteObject.GetType().GetProperty(item.Key).SetValue(incompleteObject, serializableProperty);
                    }
                }
            }
        }

        private void completeOneToManyProperty(Serializable incompleteObject, string propertyName)
        {
            Type containingTypeOfProperty = Assembly.GetExecutingAssembly().
                                            GetType(incompleteObject.GetType().
                                            GetProperty(propertyName).PropertyType.
                                            ToString().Split('[')[1].Split(']')[0]);

            Serializable containingTypeOfPropertyInstance = (Serializable)Activator.CreateInstance(containingTypeOfProperty);

            string intermediateTable = incompleteObject.getOneToManyTable(propertyName);
            string currentForeignKey = incompleteObject.getOneToManyFk(propertyName);
            string currentPrimaryKey = incompleteObject.getOneToManyPk(propertyName);

            object currentIdValue = incompleteObject.GetType().GetProperty(incompleteObject.getIdPropertyName()).
                                    GetValue(incompleteObject);

            bool isCharObject = typeof(string).IsAssignableFrom(currentIdValue.GetType()) || typeof(char).IsAssignableFrom(currentIdValue.GetType());

            string expected = isCharObject ? "'" + currentIdValue.ToString() + "'" : currentIdValue.ToString();


            string query = "select * from " + intermediateTable + " ";
            string conditionQuery = "where " + currentPrimaryKey + "=" + expected;

            if (containingTypeOfPropertyInstance.getTableName()!=intermediateTable)
            {
                query += "inner join " + containingTypeOfPropertyInstance.getTableName() +
                        " on(" + intermediateTable + "." + currentForeignKey + "=" + 
                        containingTypeOfPropertyInstance.getTableName()+"." +
                        containingTypeOfPropertyInstance.
                        getMapFromKey(containingTypeOfPropertyInstance.getIdPropertyName()) + ") ";
            }

            query += conditionQuery;

            foreach (var item in (List<object>)executeQuery(query, null, containingTypeOfProperty))
            {
                List<object> parameters = new List<object> { item};
                incompleteObject.GetType().GetProperty(propertyName).
                                PropertyType.GetMethod("Add").
                                Invoke(incompleteObject.GetType().
                                GetProperty(propertyName).
                                GetValue(incompleteObject), parameters.ToArray());
            }
        }

        private object returnValue(List<Dictionary<string,object>> dictionaryList,Type modelClassType)
        {
            if (autoMapping)
            {
                List<object> mappedList = new List<object>();

                dictionaryList.ForEach(dictionary => mappedList.Add(unSerialize(dictionary,modelClassType)));

                foreach (var item in mappedList)
                {
                    completeSerializableObject((Serializable)item);
                }

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
                    Type propertyType = objeto.GetType().GetProperty(propertyName).PropertyType;

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);

                    if (dictionary[dataName].ToString() != "")//Esto esta hardcodeado para que no setee cosas en null
                    {
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
                            object dataValue = getCastedValue(dictionary[dataName], objeto.GetType().GetProperty(propertyName).PropertyType);
                            objeto.GetType().GetProperty(propertyName).SetValue(objeto, dataValue);
                        }
                        dictionaryAux.Remove(keyToRemove);
                    }
                }
            }

            return objeto;
        }

        private object getCastedValue(object value, Type expectedType)
        {
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
            insert(objeto,objeto.getIdPropertyName(), objeto.getTableName(),false);
        }

        internal void insertCascade(Serializable objeto)
        {
            insert(objeto, objeto.getIdPropertyName(), objeto.getTableName(), true);
        }

        private void insert(Serializable objeto, String primaryKeyPropertyName, String tableName,bool cascade)
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

                    if (dataName!="")
                    {
                        insertQuery += dataName + ",";

                        valuesString += "@" + dataName + ",";

                        bool isSerializableProperty = typeof(Serializable).IsAssignableFrom(keyValuePair.Value.GetType());

                        Serializable serializableProperty = isSerializableProperty ? (Serializable)keyValuePair.Value : null;

                        if (cascade && isSerializableProperty)
                        {
                            insert(serializableProperty, serializableProperty.getIdPropertyName(), serializableProperty.getTableName(), true);
                        }

                        object parametro = isSerializableProperty ? serializableProperty.GetType().
                                            GetProperty(serializableProperty.getIdPropertyName()).
                                            GetValue(serializableProperty) : keyValuePair.Value;

                        DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                    }
                }
            }

            string primaryKeyName = objeto.getMapFromKey(objeto.getIdPropertyName());

            insertQuery = insertQuery.Remove(insertQuery.Length - 1);
            valuesString = valuesString.Remove(valuesString.Length - 1);
            insertQuery += ") " + "output inserted."+ primaryKeyName
                               + valuesString + ")";

            object insertResult = DataBase.Instance.ejecutarConsulta(
                                  insertQuery, parametros)[0][primaryKeyName];

            Type idType = objeto.GetType().GetProperty(objeto.getIdPropertyName()).PropertyType;

            object idValue = getCastedValue(insertResult, idType);

            objeto.GetType().GetProperty(objeto.getIdPropertyName()).SetValue(objeto, idValue);
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
            return selectById(id, getModelClassType());
        }

        private object selectById(object id,Type classType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(classType);

            List<object> result = selectByProperty(objeto.getIdPropertyName(), id,classType);

            return (result.Count > 0) ? result[0] : null;
        }

        internal List<object> selectByProperty(string propertyName,object propertyValue)
        {
            return selectByProperty(propertyName, propertyValue, getModelClassType());
        }

        private List<object> selectByProperty(string propertyName, object propertyValue, Type classType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(classType);

            List<SqlParameter> parameters = new List<SqlParameter>();

            bool isCharObject = typeof(string).IsAssignableFrom(propertyValue.GetType()) || typeof(char).IsAssignableFrom(propertyValue.GetType());

            string expected = isCharObject ? "'" + propertyValue.ToString() + "'" : propertyValue.ToString();

            string selectQuery = "select * from " + objeto.getTableName() + " where " +
                                objeto.getMapFromKey(propertyName) + "=" + expected;

            return executeAutoMappedSelect(selectQuery, parameters,classType);
        }

        private List<object> executeAutoMappedSelect(String selectQuery,List<SqlParameter> parameters,Type classType)
        {
            bool actualAutoMappingVal = autoMapping;//guardo el valor actual de autoMapping

            autoMapping = true;

            List<object> result = (List<object>)executeQuery(selectQuery, parameters,classType);

            autoMapping = actualAutoMappingVal;

            return result;
        }

        internal List<object> selectAll()
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(getModelClassType());

            return selectAll(objeto.getTableName(),getModelClassType());
        }

        private List<object> selectAll(String tableName,Type classType)
        {
            List<SqlParameter> parameters = new List<SqlParameter>();

            String selectQuery = "select * from " + tableName;

            return executeAutoMappedSelect(selectQuery, parameters,classType);
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
                if (keyValuePair.Key != primaryKeyPropertyName)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    if (dataName!="")
                    {
                        bool isSerializableProperty = typeof(Serializable).IsAssignableFrom(keyValuePair.Value.GetType());

                        object parametro = keyValuePair.Value;

                        if (isSerializableProperty)
                        {
                            Serializable serializableProperty = (Serializable)keyValuePair.Value;

                            parametro = serializableProperty.GetType().
                                            GetProperty(serializableProperty.getIdPropertyName()).
                                            GetValue(serializableProperty);
                        }
                        updateQuery += dataName + "=@" + dataName + ",";
                        DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                    }
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
