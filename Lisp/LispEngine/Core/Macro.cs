﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LispEngine.Datums;
using LispEngine.Evaluation;
using Environment = LispEngine.Evaluation.Environment;

namespace LispEngine.Core
{
    /**
     * Turns a function into something that
     * can be used as a macro.
     */
    internal class Macro : DatumHelpers, Function
    {
        public static readonly StackFunction Instance = new Macro().ToStack();

        private sealed class EvaluateExpansion : Task
        {
            private readonly Pair macroDatum;

            public EvaluateExpansion(Pair macroDatum)
            {
                this.macroDatum = macroDatum;
            }

            public Continuation Perform(Continuation c)
            {
                var expansion = c.Result;
                if(macroDatum != null)
                {
                    // Cache macro expansions - only incorrect
                    // if different macros are used to expand
                    // the same datum instance (which I'll have to
                    // write a test and check for...)
                    macroDatum.Cache = expansion;
                }
                c = c.PopResult();
                var env = c.Env;
                c = c.PopEnv();
                return c.Evaluate(env, expansion);
            }
        }

        private class MacroClosure : AbstractFExpression
        {
            private readonly StackFunction argFunction;

            public MacroClosure(StackFunction argFunction)
            {
                this.argFunction = argFunction;
            }

            public override Continuation Evaluate(Continuation c, Environment env, Datum args)
            {
                var p = args as Pair;
                c = c.PushEnv(env).PushTask(new EvaluateExpansion(p));
                if(p != null && p.Cache != null)
                    return c.PushResult(p.Cache);
                return argFunction.Evaluate(c, args);
            }

            public override string ToString()
            {
                return string.Format("(,macro {0})", argFunction);
            }
        }

        public static FExpression ToMacro(StackFunction function)
        {
            return new MacroClosure(function);
        }

        public Datum Evaluate(Datum args)
        {
            var a = args.ToArray();
            if (a.Length != 1)
                // TODO: Move this error boiler plate into DatumHelpers
                throw error("Incorrect arguments. Expected {0}, got {1}", 1, a.Length);
            var function = a[0] as StackFunction;
            if (function == null)
                throw error("Expected function argument");
            return ToMacro(function);
        }
    }
}
