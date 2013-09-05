﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace HybridDb.Schema
{
    public class SqlColumn
    {
        static readonly Dictionary<Type, SqlColumn> prototypes = new Dictionary<Type, SqlColumn>
        {
            {typeof (byte), new SqlColumn(DbType.Byte)},
            {typeof (sbyte), new SqlColumn(DbType.SByte)},
            {typeof (short), new SqlColumn(DbType.Int16)},
            {typeof (ushort), new SqlColumn(DbType.UInt16)},
            {typeof (int), new SqlColumn(DbType.Int32)},
            {typeof (uint), new SqlColumn(DbType.UInt32)},
            {typeof (long), new SqlColumn(DbType.Int64)},
            {typeof (ulong), new SqlColumn(DbType.UInt64)},
            {typeof (float), new SqlColumn(DbType.Single)},
            {typeof (double), new SqlColumn(DbType.Double)},
            {typeof (decimal), new SqlColumn(DbType.Decimal)},
            {typeof (bool), new SqlColumn(DbType.Boolean)},
            {typeof (string), new SqlColumn(DbType.String, Int32.MaxValue, nullable: true)},
            {typeof (char), new SqlColumn(DbType.StringFixedLength, nullable: true)},
            {typeof (Guid), new SqlColumn(DbType.Guid)},
            {typeof (DateTime), new SqlColumn(DbType.DateTime)},
            {typeof (DateTimeOffset), new SqlColumn(DbType.DateTimeOffset)},
            {typeof (TimeSpan), new SqlColumn(DbType.Time)},
            {typeof (Enum), new SqlColumn(DbType.String, 255)},
            {typeof (byte[]), new SqlColumn(DbType.Binary, Int32.MaxValue)},
            {typeof (byte?), new SqlColumn(DbType.Byte, nullable: true)},
            {typeof (sbyte?), new SqlColumn(DbType.SByte, nullable: true)},
            {typeof (short?), new SqlColumn(DbType.Int16, nullable: true)},
            {typeof (ushort?), new SqlColumn(DbType.UInt16, nullable: true)},
            {typeof (int?), new SqlColumn(DbType.Int32, nullable: true)},
            {typeof (uint?), new SqlColumn(DbType.UInt32, nullable: true)},
            {typeof (long?), new SqlColumn(DbType.Int64, nullable: true)},
            {typeof (ulong?), new SqlColumn(DbType.UInt64, nullable: true)},
            {typeof (float?), new SqlColumn(DbType.Single, nullable: true)},
            {typeof (double?), new SqlColumn(DbType.Double, nullable: true)},
            {typeof (decimal?), new SqlColumn(DbType.Decimal, nullable: true)},
            {typeof (bool?), new SqlColumn(DbType.Boolean, nullable: true)},
            {typeof (char?), new SqlColumn(DbType.StringFixedLength, nullable: true)},
            {typeof (Guid?), new SqlColumn(DbType.Guid, nullable: true)},
            {typeof (DateTime?), new SqlColumn(DbType.DateTime, nullable: true)},
            {typeof (DateTimeOffset?), new SqlColumn(DbType.DateTimeOffset, nullable: true)},
            {typeof (TimeSpan?), new SqlColumn(DbType.Time, nullable: true)},
            //{typeof (object), new SqlColumn(DbType.Object)}
        };

        public SqlColumn(Type type, bool? nullable = null)
        {
            type = (type.IsEnum) ? type.BaseType : type;
            
            SqlColumn column;
            if (!prototypes.TryGetValue(type, out column))
                throw new ArgumentException("Can only project .NET simple types, Guid, DateTime, DateTimeOffset, TimeSpan and byte[].");

            Type = column.Type;
            Length = column.Length;
            Nullable = nullable ?? column.Nullable;
            DefaultValue = column.DefaultValue;
            IsPrimaryKey = false;
        }

        public SqlColumn(DbType dbType, int? length = null, bool nullable = false, object defaultValue = null, bool isPrimaryKey = false)
        {
            Type = dbType;
            Length = length;
            Nullable = nullable;
            DefaultValue = defaultValue;
            IsPrimaryKey = isPrimaryKey;
        }

        public SqlColumn()
        {
        }

        public DbType? Type { get; internal set; }
        public int? Length { get; private set; }
        public bool Nullable { get; set; }
        public object DefaultValue { get; private set; }
        public bool IsPrimaryKey { get; private set; }

        protected bool Equals(SqlColumn other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Type == other.Type &&
                Length == other.Length &&
                Nullable.Equals(other.Nullable) &&
                Equals(DefaultValue, other.DefaultValue) &&
                IsPrimaryKey.Equals(other.IsPrimaryKey);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SqlColumn);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Type.GetHashCode();
                hashCode = (hashCode * 397) ^ Length.GetHashCode();
                hashCode = (hashCode * 397) ^ Nullable.GetHashCode();
                hashCode = (hashCode * 397) ^ (DefaultValue != null ? DefaultValue.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsPrimaryKey.GetHashCode();
                return hashCode;
            }
        }

    }
}