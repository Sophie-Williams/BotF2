using Microsoft.Scripting;

namespace Supremacy.Scripting.Ast
{
    public class TopLevelParameterInfo : IKnownVariable
    {
        public TopLevelScope Scope { get; private set; }

        SourceSpan IKnownVariable.Span
        {
            get { return Parameter.Span; }
        }

        public int Index { get; private set; }

        public Parameter Parameter
        {
            get { return Scope.Parameters[Index]; }
        }

        public TopLevelParameterInfo(TopLevelScope block, int index)
        {
            Scope = block;
            Index = index;
        }

        Scope IKnownVariable.Scope
        {
            get { return Scope; }
        }
    }
}