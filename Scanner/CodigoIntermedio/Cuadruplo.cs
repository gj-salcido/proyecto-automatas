namespace NanoRBCompiler.CodigoIntermedio
{
    /// <summary>
    /// Un cuádruplo (Ejercicio 1): tupla de 4 strings (op, arg1, arg2, resultado).
    /// Los campos que una instrucción no usa llevan "_". Se numeran en orden de ejecución, desde 1.
    ///
    /// Catálogo de operadores (Ejercicio 3):
    ///   + - * /                      (op1, op2, temporal)
    ///   == != &lt; &lt;= &gt; &gt;=           (op1, op2, temporal)     -- el resultado lógico es un temporal
    ///   =                            (valor, _, variable)
    ///   int2float                    (valor, _, temporal)
    ///   label                        (etiqueta, _, _)
    ///   goto                         (etiqueta, _, _)
    ///   if_false                     (condición, etiqueta, _)
    ///   read                         (_, _, variable)
    ///   print                        (valor, _, _)
    /// </summary>
    public class Cuadruplo
    {
        public const string Vacio = "_";

        public int Numero { get; }
        public string Op { get; }
        public string Arg1 { get; }
        public string Arg2 { get; }
        public string Resultado { get; }

        public Cuadruplo(int numero, string op, string arg1, string arg2, string resultado)
        {
            Numero = numero;
            Op = op;
            Arg1 = arg1;
            Arg2 = arg2;
            Resultado = resultado;
        }

        // Una instrucción por línea: " 4: (<=, contador, 5, t1)"
        public override string ToString()
        {
            return $"{Numero,3}: ({Op}, {Arg1}, {Arg2}, {Resultado})";
        }
    }
}
