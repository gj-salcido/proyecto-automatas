using System;
using System.Collections.Generic;
using NanoRBCompiler.Semantico;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("================ COMPILADOR NANORB - FASE 3 SEMÁNTICO ================\n");

        ProgramaEjercicio5();
        Console.WriteLine();
        ProgramaControlYEntradaSalida();
    }

    // Atajos para escribir el AST (hasta que el parser de la Fase 2 lo construya solo)
    static NodoLiteral Ent(string v, int l) => new NodoLiteral(v, TipoDato.Whole, l);
    static NodoLiteral Real(string v, int l) => new NodoLiteral(v, TipoDato.Dec, l);
    static NodoLiteral Cad(string v, int l) => new NodoLiteral(v, TipoDato.Cadena, l);
    static NodoIdentificador Id(string n, int l) => new NodoIdentificador(n, l);
    static NodoPrograma Bloque(params NodoAST[] instrucciones)
    {
        var b = new NodoPrograma();
        b.Instrucciones.AddRange(instrucciones);
        return b;
    }

    // ------------------------------------------------------------------
    //  Programa del Ejercicio 5 (el mismo texto, con estas líneas)
    //
    //   1  start
    //   2    # Declaración de variables
    //   3    whole contador
    //   4    dec total
    //   5    whole contador                 # Error 1 (Declaración doble)
    //   6    contador = 1
    //   7    total = 0.5
    //   8
    //   9    desconocida = 10               # Error 2 (Variable no declarada)
    //  10    total = desconocida + 1        # Prueba anti-cascada
    //  11
    //  12    while contador <= 5
    //  13      total = total + 1.5
    //  14      contador = contador + 1
    //  15    end
    //  16    total = "hola"                 # Error 3 (Tipos incompatibles)
    //  17    puts total
    //  18  finish
    // ------------------------------------------------------------------
    static void ProgramaEjercicio5()
    {
        Console.WriteLine("--- Programa del Ejercicio 5 ---");
        var semantico = new AnalizadorSemantico();
        var programa = new NodoPrograma();

        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "contador", 3));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Dec, "total", 4));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "contador", 5));   // ERROR 1
        programa.Instrucciones.Add(new NodoAsignacion("contador", Ent("1", 6), 6));
        programa.Instrucciones.Add(new NodoAsignacion("total", Real("0.5", 7), 7));

        programa.Instrucciones.Add(new NodoAsignacion("desconocida", Ent("10", 9), 9));   // ERROR 2

        // Anti-cascada: 'desconocida' ya es errónea, pero la suma y la asignación no agregan más errores
        programa.Instrucciones.Add(new NodoAsignacion(
            "total",
            new NodoOperacionBinaria("+", Id("desconocida", 10), Ent("1", 10), 10),
            10));

        programa.Instrucciones.Add(new NodoEstructuraControl(
            "while",
            new NodoCondicion("<=", Id("contador", 12), Ent("5", 12), 12),
            new List<NodoAST>
            {
                new NodoAsignacion("total", new NodoOperacionBinaria("+", Id("total", 13), Real("1.5", 13), 13), 13),
                new NodoAsignacion("contador", new NodoOperacionBinaria("+", Id("contador", 14), Ent("1", 14), 14), 14)
            },
            12));

        programa.Instrucciones.Add(new NodoAsignacion("total", Cad("\"hola\"", 16), 16)); // ERROR 3
        programa.Instrucciones.Add(new NodoEntradaSalida("puts", new List<NodoAST> { Id("total", 17) }, 17));

        semantico.ComprobarTipos(programa);
        semantico.ImprimirErrores();
    }

    // ------------------------------------------------------------------
    //  Cubre if / elsif / else, gets y puts con lista
    //
    //   1  start
    //   2    whole x
    //   3    dec total
    //   4    gets x
    //   5    gets y                         # y no está declarada (1 solo error)
    //   6    if x == 5
    //   7      puts "Mitad: ", x
    //   8    elsif x != 2
    //   9      total = total + 3
    //  10    else
    //  11      puts "fin"
    //  12    end
    //  13  finish
    // ------------------------------------------------------------------
    static void ProgramaControlYEntradaSalida()
    {
        Console.WriteLine("--- if / elsif / else, gets y puts con lista ---");
        var semantico = new AnalizadorSemantico();
        var programa = new NodoPrograma();

        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "x", 2));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Dec, "total", 3));
        programa.Instrucciones.Add(new NodoEntradaSalida("gets", new List<NodoAST> { Id("x", 4) }, 4));
        programa.Instrucciones.Add(new NodoEntradaSalida("gets", new List<NodoAST> { Id("y", 5) }, 5));   // ERROR

        var bloqueElse = Bloque(
            new NodoEntradaSalida("puts", new List<NodoAST> { Cad("\"fin\"", 11) }, 11));

        var ramaElsif = new NodoEstructuraControl(
            "if",
            new NodoCondicion("!=", Id("x", 8), Ent("2", 8), 8),
            new List<NodoAST>
            {
                new NodoAsignacion("total", new NodoOperacionBinaria("+", Id("total", 9), Ent("3", 9), 9), 9)
            },
            8,
            bloqueElse);

        programa.Instrucciones.Add(new NodoEstructuraControl(
            "if",
            new NodoCondicion("==", Id("x", 6), Ent("5", 6), 6),
            new List<NodoAST>
            {
                new NodoEntradaSalida("puts", new List<NodoAST> { Cad("\"Mitad: \"", 7), Id("x", 7) }, 7)
            },
            6,
            ramaElsif));

        semantico.ComprobarTipos(programa);
        semantico.ImprimirErrores();
    }
}
