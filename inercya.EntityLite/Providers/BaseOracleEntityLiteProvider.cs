﻿/*
Copyright 2014 i-nercya intelligent software

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using inercya.EntityLite.Builders;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using inercya.EntityLite.Extensions;

namespace inercya.EntityLite.Providers
{
    public abstract class BaseOracleEntityLiteProvider : EntityLiteProvider
    {

        protected BaseOracleEntityLiteProvider(DataService dataService): base(dataService)
        {
        }


        public override string ParameterPrefix
        {
            get { return ":"; }
        }


        public override AutoGeneratedFieldFetchMode AutoGeneratedFieldFetchMode
        {
            get { return AutoGeneratedFieldFetchMode.OutputParameter; }
        }

        public override string GetPagedQuery(AbstractQueryBuilder builder, DbCommand selectCommand, ref int paramIndex, int fromRowIndex, int toRowIndex)
        {
            /*
SELECT *
FROM (
  SELECT od.*, rownum AS RowNumber__
  FROM order_details od
) T
WHERE RowNumber__ between 10 and 19;  
             */

            var commandText = new StringBuilder();
            if (builder.QueryLite.Alias == null) builder.QueryLite.Alias = new Alias("IT", builder.QueryLite.EntityType);
            var aliasName = builder.QueryLite.Alias.Name;
            string columnList = builder.GetColumnList();
            bool isStar = columnList == "*";
            commandText.Append("SELECT ");
            if (isStar) commandText.Append("*");
            else commandText.NewIndentedLine(1).Append(columnList);
            commandText.Append("\n")
                       .Append("FROM")
                       .NewIndentedLine(1).Append('(')
                       .NewIndentedLine(2).Append("SELECT ").Append(aliasName).Append(".*, rownum AS row_number__")
                       .NewIndentedLine(2).Append("FROM ")
                       .NewIndentedLine(3).Append(builder.GetFromClauseContent(selectCommand, ref paramIndex, 3));
            bool hasWhereClause = builder.QueryLite.Filter != null && !builder.QueryLite.Filter.IsEmpty();
            if (hasWhereClause)
            {
                commandText.NewIndentedLine(2).Append("WHERE").NewIndentedLine(3).Append(builder.GetFilter(selectCommand, ref paramIndex, builder.QueryLite.Filter, 3, false));
            }
            if (builder.QueryLite.Sort != null && builder.QueryLite.Sort.Count > 0)
            {
                commandText.NewIndentedLine(2).Append("ORDER BY").NewIndentedLine(3).Append(builder.GetSort());
            }
            commandText.NewIndentedLine(1).Append(") T\n");
            string fromParameterName;
            IDbDataParameter fromParameter = builder.CreateIn32Parameter(fromRowIndex + 1, ref paramIndex, out fromParameterName);
            selectCommand.Parameters.Add(fromParameter);
            string toParameterName;
            IDbDataParameter toParameter = builder.CreateIn32Parameter(toRowIndex + 1, ref paramIndex, out toParameterName);
            selectCommand.Parameters.Add(toParameter);
            commandText.Append("WHERE")
                .NewIndentedLine(1).Append("row_number__ BETWEEN ")
                .Append(fromParameterName)
                .Append(" AND ").Append(toParameterName);
            return commandText.ToString();
        }

        public override string SequenceVariable
        {
            get
            {
                return "id_seq_$var$";
            }
        }

        protected override DbCommand GenerateInsertCommandWithAutogeneratedField(CommandBuilder commandBuilder, object entity, EntityMetadata entityMetadata)
        {
            var cmd = this.CreateCommand();
            StringBuilder commandText = new StringBuilder();
            commandText.Append(string.Format(@"
DECLARE
    {0} NUMERIC(18);
BEGIN
    {0} := {1};", SequenceVariable, GetNextValExpression( entityMetadata.GetFullSequenceName(this.DefaultSchema))));
            commandBuilder.AppendInsertStatement(entity, cmd, commandText);
            commandText.Append(string.Format(@";
    :id_seq_$param$ := {0};
END;", SequenceVariable));
            IDbDataParameter idp = cmd.CreateParameter();
            idp.ParameterName = ":id_seq_$param$";
            idp.Direction = ParameterDirection.Output;
            idp.DbType = DbType.Int64;
            cmd.Parameters.Add(idp);
            cmd.CommandText = commandText.ToString();
            return cmd;
        }

        public override string GetNextValExpression(string fullSequenceName)
        {
            return fullSequenceName + ".nextval";
        }

        protected override void AppendGetAutoincrementField(StringBuilder commandText, EntityMetadata entityMetadata)
        {
            throw new NotImplementedException();
        }

        public override string DualTable
        {
            get
            {
                return "DUAL";
            }
        }

        private static PropertySetter OracleDbTypeSetter;

        public override void SetProviderTypeToParameter(IDbDataParameter parameter, int providerType)
        {
            if (OracleDbTypeSetter == null)
            {
                var parameterType = parameter.GetType();
                var pi = parameterType.GetProperty("OracleDbType");
                if (pi == null) new InvalidOperationException("OracleDbType property not found on type " + parameterType.FullName);
                OracleDbTypeSetter = PropertyHelper.GetPropertySetter(pi);
            }
            OracleDbTypeSetter(parameter, providerType);
        }
    }
}