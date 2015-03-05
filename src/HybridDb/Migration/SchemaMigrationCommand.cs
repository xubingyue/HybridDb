using System;
using System.Data;
using System.Data.SqlClient;
using HybridDb.Schema;

namespace HybridDb.Migration
{
    public abstract class SchemaMigrationCommand
    {
        protected SchemaMigrationCommand()
        {
            Unsafe = false;
            NeedsReprojection = false;
        }

        public bool Unsafe { get; protected set; }
        public bool NeedsReprojection { get; protected set; }

        public abstract void Execute(Database db);

        protected string GetTableExistsSql(Database db, string tablename)
        {
            return string.Format(db.TableMode == TableMode.UseRealTables
                ? "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')"
                : "OBJECT_ID('tempdb..{0}') is not null",
                db.FormatTableName(tablename));
        }

        protected SqlBuilder GetColumnSqlType(Column column)
        {
            if (column.SqlColumn.Type == null)
                throw new ArgumentException(string.Format("Column {0} must have a type", column.Name));

            var sql = new SqlBuilder();

            sql.Append(new SqlParameter { DbType = (DbType)column.SqlColumn.Type }.SqlDbType.ToString());
            sql.Append(column.SqlColumn.Length != null, "(" + (column.SqlColumn.Length == Int32.MaxValue ? "MAX" : column.SqlColumn.Length.ToString()) + ")");
            sql.Append(column.SqlColumn.Nullable, "NULL").Or("NOT NULL");
            sql.Append(column.SqlColumn.DefaultValue != null,
                       "DEFAULT @DefaultValue",
                       new Parameter
                       {
                           Name = "DefaultValue",
                           DbType = column.SqlColumn.Type,
                           Value = column.SqlColumn.DefaultValue
                       });
            sql.Append(column.SqlColumn.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }
    }
}