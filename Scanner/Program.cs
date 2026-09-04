using System;
using System.Collections.Generic;
using NanoRBCompiler.Scanner;
using NanoRBCompiler.TablaSimbolos;
using NanoRBCompiler.Errores;

class Program
{
    static void Main(string[] args)
    {
        string codigoFuente = @"start
  # Declaración de variables
  whole contador
  dec total
  
  contador = 1
  total = 0.5
  
  while contador <= 5
    total = total + 1.5
    contador = contador + 1
  end
  
  puts total
finish";

        var manejadorErrores = new ManejadorErrores();
        var scanner = new ScannerEngine(codigoFuente, manejadorErrores);
        var tablaSimbolos = new TablaSimbolosManager();

        Console.WriteLine("================ TIRA DE TOKENS (SALIDA) ================");
        List<Token> tokens = scanner.EscanearTodo();

        foreach (var token in tokens)
        {
            Console.WriteLine(token);

            if (token.Codigo == 100)
            {
                tablaSimbolos.RegistrarIdentificador(token.Lexema, token.Codigo, token.Linea);
            }
        }

        tablaSimbolos.ImprimirTabla();
        manejadorErrores.ImprimirErrores();
    }
}