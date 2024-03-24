using System;

namespace UndertaleModLib.Decompiler;

public static partial class Decompiler
{
    // Assignment statement for tempvars.
    public class TempVarAssignmentStatement : Statement
    {
        public TempVarReference Var;
        public Expression Value;

        public bool HasVarKeyword;

        public TempVarAssignmentStatement(TempVarReference var, Expression value)
        {
            Var = var;
            Value = value;
        }

        public override string ToString(DecompileContext context)
        {
            //TODO: why is there a GMS2Check for this? var exists in gms1.4 as well
            if (context.GlobalContext.Data?.IsGameMaker2() ?? false && !HasVarKeyword && context.LocalVarDefinesUsed.Peek().Add(Var.Var.Name)) {
                HasVarKeyword = true;
                context.LocalVarDefines.Add(Var.Var.Name);
            }

            return String.Format("{0}{1} = {2}", (HasVarKeyword ? "var " : ""), Var.Var.Name, Value.ToString(context));
        }

        public override Statement CleanStatement(DecompileContext context, BlockHLStatement block)
        {
            Value = Value?.CleanExpression(context, block);

            if (Var != null && Var.Var != null && Var.Var.Name != null)
            {
                if ((Value as ExpressionTempVar)?.Var?.Var?.Name == Var.Var.Name) // This is literally set to itself. No thank you.
                {
                    block.Statements.Remove(context.TempVarMap[Var.Var.Name]);
                    return context.TempVarMap[Var.Var.Name].CleanStatement(context, block);
                }

                context.TempVarMap[Var.Var.Name] = this;
            }
            return this;
        }

        internal override AssetIDType DoTypePropagation(DecompileContext context, AssetIDType suggestedType)
        {
            if (Var.Var.AssetType == AssetIDType.Other)
                Var.Var.AssetType = suggestedType;
            AssetIDType type = Value.DoTypePropagation(context, Var.Var.AssetType);
            if (Var.Var.AssetType == AssetIDType.Other)
                Var.Var.AssetType = type;
            return type;
        }
    }
}