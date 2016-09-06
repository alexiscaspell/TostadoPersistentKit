﻿using System;
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

        internal object returnValue(List<Dictionary<string,object>> dictionaryList)
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

        internal Serializable unSerialize(Dictionary<string, object> dictionary,Type modelClassType)
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
                        objeto.GetType().GetProperty(propertyName).SetValue(objeto, dictionary[dataName]);
                    }
                }
            }

            return objeto;
        }

        internal List<String> listSerializableProperties(object objeto)
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

    }
}
