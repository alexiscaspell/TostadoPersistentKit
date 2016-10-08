# TostadoPersistentKit
Conjunto de clases programadas en C# para facilitar el uso de bd

##Para usarlo :

- Copiar las carpetas "Annotations" y "Classes" al proyecto.
- Copiar el archivo "configuracion_sistema.txt" en la carpeta raiz.

###Repository :

####Esta clase abstracta la tiene que implementar cualquier dao, aca unas aclaraciones :

- Para usarse se tiene que especificar a que clase va a hacerse de repositorio, esto se hace implementando el metodo getModelClassType retornando typeof(ClaseQueMapeo).

- Repository de por si ya tiene hechos los metodos insert,update,delete y algunos select

- Se puede ejecutar cualquier stored o query (executeStored(),executeQuery()) y aun asi el resultado se intentara mapear contra un List(ClaseQueMapeo).

- En el caso de que no se quiera usar el automapeo se puede setear la propiedad autoMapping=false esto hace que cualquier metodo de Repository me devuelva un List(Dictionary(string,object)) que simula ser una tabla (si solo se quiere ejecutar un solo metodo con autoMapping en false, luego de ejecutarlo volver a setearla en true)

###Serializable :

####Toda clase que se quiera mapear debe implementar la clase Serializable y utilizar las siguientes annotations :

- [Table(name)] : Esta va encima de la declaracion de la clase; name es el nombre de la tabla contra la que se va a mapear(en el caso de no especificar un name, por default es el nombre de la clase).
- [Id(name,type)] :Esta annotation va sobre la propiedad que mapea la pk, name es el nombre de la columna en la tabla y type es el tipo de pk(natural o subrogada).
- [Column(name,fetch)] : Va sobre cualquier propiedad mapeada contra alguna columna, name es el nombre de columna y fetch es el tipo de busqueda que se le da (lazy o eager).
- [OneToMany(pkName,tableName,fkName,fetch)] : Esta annotation se usa sobre cualquier propiedad que sea OneToMany (visto desde la tabla actual), pkName es el nombre de la fk de la tabla que referencia a esta, tableName es el nombre de la tabla que referencia a esta y fkName es el nombre de la pk de la tabla de la propiedad.

###DefaultDatabaseCreator :

####Adicionalmente esta clase sirve para facilitar la tarea de generar las tablas, contiene los siguientes metodos :

- createPersistentDefaultModel(): Este metodo busca todas las clases que implementen la clase abstracta Serializable ddentro del proyecto y genera el modelo de datos correspondiente en la bd especificada en el archivo de configuracion, en el caso de pasarle como parametro el valor true ejecuta dropExistingTables() antes de crear las tablas.
- dropExistingTables(): Este metodo borra todas las tablas de la bd que se llamen igual a las tablas mapeadas en el proyecto.


###Aclaraciones :

- Si se tiene una propiedad que es Serializable y no se especifico un FetchType, este sera por valor default LAZY.
- Si se tiene una relacion ManyToMany con una propiedad, se la implementa con la annotation OneToMany (Esto por ahora NO funciona)
- Repository contiene ademas los metodos insertCascade y updateCascade que realizan un insert/update recursivo (A excepcion de las propiedades OneToMany)



