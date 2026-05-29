using System;
using System.Collections.Generic;
using System.Globalization;

namespace VectorField
{
    /// <summary>
    /// Compila y evalúa expresiones matemáticas de texto con variables x,y.
    /// Soporta: + - * / ^, paréntesis, constantes (pi,e) y funciones: sin,cos,tan,asin,acos,atan,sqrt,abs,exp,ln,log,log10.
    /// Pensado para visualización de campos vectoriales (James Stewart: F(x,y)=<P(x,y),Q(x,y)>).
    /// </summary>
    public static class ExpressionCompiler
    {
        public sealed class CompiledExpression
        {
            readonly RpnToken[] _rpn;

            internal CompiledExpression(RpnToken[] rpn) => _rpn = rpn;

            public float Evaluate(float x, float y)
            {
                // stack en double para estabilidad numérica
                // Nota: evitamos Span/stackalloc por compatibilidad entre backends de Unity.
                double[] stack = new double[64];
                int sp = 0;

                for (int i = 0; i < _rpn.Length; i++)
                {
                    var t = _rpn[i];
                    switch (t.Kind)
                    {
                        case RpnKind.Number:
                            stack[sp++] = t.Number;
                            break;
                        case RpnKind.VarX:
                            stack[sp++] = x;
                            break;
                        case RpnKind.VarY:
                            stack[sp++] = y;
                            break;
                        case RpnKind.ConstPi:
                            stack[sp++] = Math.PI;
                            break;
                        case RpnKind.ConstE:
                            stack[sp++] = Math.E;
                            break;

                        case RpnKind.Negate:
                        {
                            if (sp < 1) return 0f;
                            stack[sp - 1] = -stack[sp - 1];
                            break;
                        }
                        case RpnKind.Add:
                        case RpnKind.Sub:
                        case RpnKind.Mul:
                        case RpnKind.Div:
                        case RpnKind.Pow:
                        {
                            if (sp < 2) return 0f;
                            double b = stack[--sp];
                            double a = stack[--sp];
                            double r = t.Kind switch
                            {
                                RpnKind.Add => a + b,
                                RpnKind.Sub => a - b,
                                RpnKind.Mul => a * b,
                                RpnKind.Div => b == 0d ? 0d : a / b,
                                RpnKind.Pow => Math.Pow(a, b),
                                _ => 0d
                            };
                            stack[sp++] = r;
                            break;
                        }

                        case RpnKind.FuncSin:
                        case RpnKind.FuncCos:
                        case RpnKind.FuncTan:
                        case RpnKind.FuncAsin:
                        case RpnKind.FuncAcos:
                        case RpnKind.FuncAtan:
                        case RpnKind.FuncSqrt:
                        case RpnKind.FuncAbs:
                        case RpnKind.FuncExp:
                        case RpnKind.FuncLn:
                        case RpnKind.FuncLog:
                        case RpnKind.FuncLog10:
                        {
                            if (sp < 1) return 0f;
                            double a = stack[sp - 1];
                            double r = t.Kind switch
                            {
                                RpnKind.FuncSin => Math.Sin(a),
                                RpnKind.FuncCos => Math.Cos(a),
                                RpnKind.FuncTan => Math.Tan(a),
                                RpnKind.FuncAsin => Math.Asin(a),
                                RpnKind.FuncAcos => Math.Acos(a),
                                RpnKind.FuncAtan => Math.Atan(a),
                                RpnKind.FuncSqrt => a < 0d ? 0d : Math.Sqrt(a),
                                RpnKind.FuncAbs => Math.Abs(a),
                                RpnKind.FuncExp => Math.Exp(a),
                                RpnKind.FuncLn => a <= 0d ? 0d : Math.Log(a),
                                // log = log natural (calculo). log10 explícito también.
                                RpnKind.FuncLog => a <= 0d ? 0d : Math.Log(a),
                                RpnKind.FuncLog10 => a <= 0d ? 0d : Math.Log10(a),
                                _ => 0d
                            };
                            stack[sp - 1] = r;
                            break;
                        }
                    }

                    if (sp >= stack.Length)
                        return 0f;
                }

                if (sp != 1)
                    return 0f;

                double result = stack[0];
                if (double.IsNaN(result) || double.IsInfinity(result))
                    return 0f;

                // clamp suave para evitar escalas absurdas en flechas
                if (result > 1e6) result = 1e6;
                if (result < -1e6) result = -1e6;

                return (float)result;
            }
        }

        enum TokenKind
        {
            Number,
            Ident,
            Plus,
            Minus,
            Star,
            Slash,
            Caret,
            LParen,
            RParen,
            Comma,
        }

        readonly struct Token
        {
            public readonly TokenKind Kind;
            public readonly double Number;
            public readonly string Text;

            public Token(TokenKind kind, double number, string text)
            {
                Kind = kind;
                Number = number;
                Text = text;
            }
        }

        enum Assoc { Left, Right }

        enum OpKind
        {
            Add,
            Sub,
            Mul,
            Div,
            Pow,
            Negate,
        }

        internal enum RpnKind
        {
            Number,
            VarX,
            VarY,
            ConstPi,
            ConstE,
            Add,
            Sub,
            Mul,
            Div,
            Pow,
            Negate,
            FuncSin,
            FuncCos,
            FuncTan,
            FuncAsin,
            FuncAcos,
            FuncAtan,
            FuncSqrt,
            FuncAbs,
            FuncExp,
            FuncLn,
            FuncLog,
            FuncLog10,
        }

        internal readonly struct RpnToken
        {
            public readonly RpnKind Kind;
            public readonly double Number;

            public RpnToken(RpnKind kind, double number = 0d)
            {
                Kind = kind;
                Number = number;
            }
        }

        readonly struct OpInfo
        {
            public readonly OpKind Kind;
            public readonly int Precedence;
            public readonly Assoc Assoc;

            public OpInfo(OpKind kind, int precedence, Assoc assoc)
            {
                Kind = kind;
                Precedence = precedence;
                Assoc = assoc;
            }
        }

        public static bool TryCompile(string expression, out CompiledExpression compiled, out string error)
        {
            compiled = null;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "Expresión vacía";
                return false;
            }

            if (!TryTokenize(expression, out var tokens, out error))
                return false;

            if (!TryToRpn(tokens, out var rpn, out error))
                return false;

            compiled = new CompiledExpression(rpn);
            return true;
        }

        static bool TryTokenize(string src, out List<Token> tokens, out string error)
        {
            tokens = new List<Token>(64);
            error = null;

            int i = 0;
            while (i < src.Length)
            {
                char ch = src[i];

                if (char.IsWhiteSpace(ch)) { i++; continue; }

                if (char.IsDigit(ch) || ch == '.' || ch == ',')
                {
                    int start = i;
                    bool hasSep = false;
                    while (i < src.Length)
                    {
                        char c = src[i];
                        if (char.IsDigit(c)) { i++; continue; }
                        if ((c == '.' || c == ',') && !hasSep) { hasSep = true; i++; continue; }
                        break;
                    }

                    string numText = src.Substring(start, i - start).Replace(',', '.');
                    if (!double.TryParse(numText, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                    {
                        error = $"Número inválido: '{numText}'";
                        return false;
                    }
                    tokens.Add(new Token(TokenKind.Number, n, null));
                    continue;
                }

                if (char.IsLetter(ch) || ch == '_')
                {
                    int start = i;
                    while (i < src.Length)
                    {
                        char c = src[i];
                        if (char.IsLetterOrDigit(c) || c == '_') { i++; continue; }
                        break;
                    }
                    string ident = src.Substring(start, i - start);
                    tokens.Add(new Token(TokenKind.Ident, 0d, ident));
                    continue;
                }

                switch (ch)
                {
                    case '+': tokens.Add(new Token(TokenKind.Plus, 0, null)); i++; break;
                    case '-': tokens.Add(new Token(TokenKind.Minus, 0, null)); i++; break;
                    case '*': tokens.Add(new Token(TokenKind.Star, 0, null)); i++; break;
                    case '/': tokens.Add(new Token(TokenKind.Slash, 0, null)); i++; break;
                    case '^': tokens.Add(new Token(TokenKind.Caret, 0, null)); i++; break;
                    case '(': tokens.Add(new Token(TokenKind.LParen, 0, null)); i++; break;
                    case ')': tokens.Add(new Token(TokenKind.RParen, 0, null)); i++; break;
                    case ',': tokens.Add(new Token(TokenKind.Comma, 0, null)); i++; break;
                    default:
                        error = $"Carácter no soportado: '{ch}'";
                        return false;
                }
            }

            return true;
        }

        static bool TryToRpn(List<Token> tokens, out RpnToken[] rpn, out string error)
        {
            error = null;
            var output = new List<RpnToken>(tokens.Count);
            var ops = new Stack<object>(32); // OpInfo o string (func) o '(' marker

            bool expectingValue = true;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                switch (t.Kind)
                {
                    case TokenKind.Number:
                        output.Add(new RpnToken(RpnKind.Number, t.Number));
                        expectingValue = false;
                        break;

                    case TokenKind.Ident:
                    {
                        string id = t.Text;
                        if (IsVariable(id, out var varKind))
                        {
                            output.Add(new RpnToken(varKind));
                            expectingValue = false;
                        }
                        else if (IsConstant(id, out var constKind))
                        {
                            output.Add(new RpnToken(constKind));
                            expectingValue = false;
                        }
                        else if (IsFunction(id, out _))
                        {
                            ops.Push(id);
                            expectingValue = true;
                        }
                        else
                        {
                            error = $"Identificador no reconocido: '{id}'. Usa x, y, pi, e o funciones como sin/cos.";
                            rpn = null;
                            return false;
                        }
                        break;
                    }

                    case TokenKind.Plus:
                    case TokenKind.Minus:
                    case TokenKind.Star:
                    case TokenKind.Slash:
                    case TokenKind.Caret:
                    {
                        OpInfo op;
                        if (t.Kind == TokenKind.Minus && expectingValue)
                        {
                            op = new OpInfo(OpKind.Negate, 4, Assoc.Right);
                        }
                        else
                        {
                            op = t.Kind switch
                            {
                                TokenKind.Plus => new OpInfo(OpKind.Add, 1, Assoc.Left),
                                TokenKind.Minus => new OpInfo(OpKind.Sub, 1, Assoc.Left),
                                TokenKind.Star => new OpInfo(OpKind.Mul, 2, Assoc.Left),
                                TokenKind.Slash => new OpInfo(OpKind.Div, 2, Assoc.Left),
                                TokenKind.Caret => new OpInfo(OpKind.Pow, 3, Assoc.Right),
                                _ => default
                            };
                        }

                        while (ops.Count > 0)
                        {
                            object top = ops.Peek();
                            if (top is OpInfo topOp)
                            {
                                bool pop = op.Assoc == Assoc.Left
                                    ? op.Precedence <= topOp.Precedence
                                    : op.Precedence < topOp.Precedence;

                                if (!pop) break;
                                ops.Pop();
                                EmitOp(output, topOp.Kind);
                                continue;
                            }
                            break;
                        }

                        ops.Push(op);
                        expectingValue = true;
                        break;
                    }

                    case TokenKind.LParen:
                        ops.Push('(');
                        expectingValue = true;
                        break;

                    case TokenKind.RParen:
                    {
                        bool found = false;
                        while (ops.Count > 0)
                        {
                            object top = ops.Pop();
                            if (top is char c && c == '(')
                            {
                                found = true;
                                break;
                            }
                            if (top is OpInfo topOp)
                            {
                                EmitOp(output, topOp.Kind);
                                continue;
                            }
                            if (top is string)
                            {
                                // funciones no deberían aparecer antes del '('
                                // (se emiten cuando cerramos paréntesis)
                                continue;
                            }
                        }

                        if (!found)
                        {
                            error = "Paréntesis desbalanceados";
                            rpn = null;
                            return false;
                        }

                        // Si lo que queda arriba es una función, emitirla (sin(x))
                        if (ops.Count > 0 && ops.Peek() is string func)
                        {
                            ops.Pop();
                            if (!EmitFunc(output, func, out error))
                            {
                                rpn = null;
                                return false;
                            }
                        }

                        expectingValue = false;
                        break;
                    }

                    case TokenKind.Comma:
                        // No soportamos funciones con múltiples args por ahora.
                        error = "No se soportan funciones con múltiples argumentos (coma).";
                        rpn = null;
                        return false;

                    default:
                        error = "Token inválido";
                        rpn = null;
                        return false;
                }
            }

            while (ops.Count > 0)
            {
                object top = ops.Pop();
                if (top is char c && c == '(')
                {
                    error = "Paréntesis desbalanceados";
                    rpn = null;
                    return false;
                }
                if (top is string func)
                {
                    if (!EmitFunc(output, func, out error))
                    {
                        rpn = null;
                        return false;
                    }
                    continue;
                }
                if (top is OpInfo op)
                {
                    EmitOp(output, op.Kind);
                    continue;
                }
            }

            if (output.Count == 0)
            {
                error = "Expresión vacía";
                rpn = null;
                return false;
            }

            rpn = output.ToArray();
            return true;
        }

        static bool IsVariable(string ident, out RpnKind kind)
        {
            if (string.Equals(ident, "x", StringComparison.OrdinalIgnoreCase)) { kind = RpnKind.VarX; return true; }
            if (string.Equals(ident, "y", StringComparison.OrdinalIgnoreCase)) { kind = RpnKind.VarY; return true; }
            kind = default;
            return false;
        }

        static bool IsConstant(string ident, out RpnKind kind)
        {
            if (string.Equals(ident, "pi", StringComparison.OrdinalIgnoreCase)) { kind = RpnKind.ConstPi; return true; }
            if (string.Equals(ident, "e", StringComparison.OrdinalIgnoreCase)) { kind = RpnKind.ConstE; return true; }
            kind = default;
            return false;
        }

        static bool IsFunction(string ident, out RpnKind kind)
        {
            kind = ident.ToLowerInvariant() switch
            {
                "sin" => RpnKind.FuncSin,
                "cos" => RpnKind.FuncCos,
                "tan" => RpnKind.FuncTan,
                "asin" => RpnKind.FuncAsin,
                "acos" => RpnKind.FuncAcos,
                "atan" => RpnKind.FuncAtan,
                "sqrt" => RpnKind.FuncSqrt,
                "abs" => RpnKind.FuncAbs,
                "exp" => RpnKind.FuncExp,
                "ln" => RpnKind.FuncLn,
                "log" => RpnKind.FuncLog,
                "log10" => RpnKind.FuncLog10,
                _ => default
            };

            return kind != default;
        }

        static void EmitOp(List<RpnToken> output, OpKind op)
        {
            output.Add(op switch
            {
                OpKind.Add => new RpnToken(RpnKind.Add),
                OpKind.Sub => new RpnToken(RpnKind.Sub),
                OpKind.Mul => new RpnToken(RpnKind.Mul),
                OpKind.Div => new RpnToken(RpnKind.Div),
                OpKind.Pow => new RpnToken(RpnKind.Pow),
                OpKind.Negate => new RpnToken(RpnKind.Negate),
                _ => new RpnToken(RpnKind.Number, 0d)
            });
        }

        static bool EmitFunc(List<RpnToken> output, string funcName, out string error)
        {
            error = null;
            if (!IsFunction(funcName, out var kind))
            {
                error = $"Función no soportada: '{funcName}'";
                return false;
            }

            output.Add(new RpnToken(kind));
            return true;
        }
    }
}
