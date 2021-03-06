using System.ComponentModel;
using Supremacy.Scripting.Runtime;

namespace Supremacy.Scripting.Ast
{
    public class WhereClause : QueryClause
    {

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Expression Predicate
        {
            get { return Expression; }
            set { Expression = value; }
        }

        public Expression Body
        {
            get { return null; }
            set { return; }
        }

        public override void Dump(SourceWriter sw, int indentChange)
        {
            sw.Write("where ");

            var indentShift = "where ".Length;

            sw.Indent += indentShift;

            try
            {
                DumpChild(Expression, sw, indentChange);
            }
            finally
            {
                sw.Indent -= indentShift;
            }

            sw.WriteLine();

            DumpChild(Next, sw, indentChange);
        }

        protected override string MethodName
        {
            get { return "Where"; }
        }
    }
}