namespace NanoRBCompiler.Scanner
{
    public class Token
    {
        public int Codigo { get; }
        public string Lexema { get; }
        public int Linea { get; }

        public Token(int codigo, string lexema, int linea)
        {
            Codigo = codigo;
            Lexema = lexema ?? string.Empty;
            Linea = linea;
        }

        public override string ToString()
        {
            return $"[Línea {Linea,3}] Token {Codigo,3} | Lexema: \"{Lexema}\"";
        }
    }
}