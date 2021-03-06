using System;

using Microsoft.Scripting;

using Supremacy.Annotations;

using System.Linq;

using Supremacy.Scripting.Runtime;
using Supremacy.Scripting.Utility;

namespace Supremacy.Scripting.Ast
{
    public class TypeExpression : FullNamedExpression
    {
        public override bool IsPrimaryExpression
        {
            get { return true; }
        }

        protected TypeExpression() {}

        public TypeExpression(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            Type = type;
        }

        public TypeExpression(Type type, SourceSpan span)
            : this(type)
        {
            Span = span;
        }

        public static TypeExpression Create([NotNull] Type type, SourceSpan span = default(SourceSpan))
        {
            if (type == null)
                throw new ArgumentNullException("type");
            
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                return new GenericTypeExpression(
                    type,
                    new TypeArguments(type.GetGenericArguments().Select(o => Create(o, span)).ToArray()),
                    span);
            }

            return new TypeExpression(type, span);
        }

        public override System.Linq.Expressions.Expression TransformCore(ScriptGenerator generator)
        {
            return System.Linq.Expressions.Expression.Constant(
                Type,
                typeof(Type));
        }

        public override void Dump(SourceWriter sw, int indentChange)
        {
            sw.Write(TypeManager.GetCSharpName(Type));
        }

        public override FullNamedExpression ResolveAsTypeStep(ParseContext ec, bool silent)
        {
            var resolvedType = DoResolveAsTypeStep(ec);
            if (resolvedType == null)
                return null;

            ExpressionClass = ExpressionClass.Type;
            return resolvedType;
        }

        protected virtual TypeExpression DoResolveAsTypeStep(ParseContext parseContext)
        {
            return this;
        }

        public virtual bool CheckAccessLevel(ParseContext parseContext)
        {
            return TypeManager.CheckAccessLevel(parseContext, Type);
        }


        public virtual bool IsClass
        {
            get { return Type.IsClass; }
        }

        public virtual bool IsValueType
        {
            get { return TypeManager.IsStruct(Type); }
        }

        public virtual bool IsInterface
        {
            get { return Type.IsInterface; }
        }

        public virtual bool IsSealed
        {
            get { return Type.IsSealed; }
        }

        public virtual bool CanInheritFrom()
        {
            if (Type == TypeManager.CoreTypes.Enum ||
                Type == TypeManager.CoreTypes.ValueType ||
                Type == TypeManager.CoreTypes.MulticastDelegate ||
                Type == TypeManager.CoreTypes.Delegate ||
                Type == TypeManager.CoreTypes.Array)
            {
                return false;
            }
            return true;
        }
    }
}