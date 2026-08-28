using LPR381_Project.Algorithms;
using LPR381_Project.IO;
using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project
{
    internal class Program
    {
        private LPModel _model;
        private Solution _lastSolution;
        private readonly OutputWriter _output = new OutputWriter();

        enum MainMenu
        {
            Exit = 0,
            LoadFile = 1,
            Solve = 2,
            Sensitivity = 3,
            SaveOutput = 4
        }

        enum AlgorithmMenu
        {
            PrimalSimplex = 1,
            RevisedPrimalSimplex = 2,
            BranchAndBoundSimplex = 3,
            CuttingPlane = 4,
            BranchAndBoundKnapsack = 5
        }

        static void Main(string[] args)
        {
            new Program().Run();
        }

        void Run()
        {
            Console.WriteLine("=== LPR381 Linear & Integer Programming Solver ===");

            while (true)
            {
                foreach (var name in Enum.GetValues(typeof(MainMenu)))
                {
                    Console.WriteLine((int)name + ". " + name);
                }

                if (!Enum.TryParse(Console.ReadLine()?.Trim(), out MainMenu choice))
                {
                    Console.WriteLine("Invalid choice.");
                    continue;
                }

                switch (choice)
                {
                    case MainMenu.LoadFile: LoadModel(); break;
                    case MainMenu.Solve: SolveMenu(); break;
                    case MainMenu.Sensitivity: SensitivityMenu(); break;
                    case MainMenu.SaveOutput: SaveOutput(); break;
                    case MainMenu.Exit: return;
                    default: Console.WriteLine("Invalid choice."); break;
                }
            }
        }

        private void LoadModel()
        {
            Console.Write("Path to input file: ");
            var path = Console.ReadLine() ?? "";
            try
            {
                _model = InputParser.Parse(path);
                Console.WriteLine($"Loaded model: {_model.VariableCount} variable(s), {_model.Constraints.Count} constraint(s).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load input file: {ex.Message}");
            }
        }

        private void SolveMenu()
        {
            if (_model == null)
            {
                Console.WriteLine("Load an input file first.");
                return;
            }

            foreach (var name in Enum.GetValues(typeof(AlgorithmMenu)))
            {
                Console.WriteLine((int)name + ". " + name);
            }

            if (!Enum.TryParse(Console.ReadLine()?.Trim(), out AlgorithmMenu choice))
            {
                Console.WriteLine("Invalid algorithm choice.");
                return;
            }

            IAlgorithm algorithm = null;

            switch (choice)
            {
                case AlgorithmMenu.PrimalSimplex:
                    algorithm = new PrimalSimplex();
                    break;
                case AlgorithmMenu.RevisedPrimalSimplex:
                    algorithm = new RevisedPrimalSimplex();
                    break;
                case AlgorithmMenu.BranchAndBoundSimplex:
                    algorithm = new BranchAndBoundSimplex();
                    break;
                case AlgorithmMenu.CuttingPlane:
                    algorithm = new CuttingPlane();
                    break;
                case AlgorithmMenu.BranchAndBoundKnapsack:
                    algorithm = new BranchAndBoundKnapsack();
                    break;
                default:
                    algorithm = null;
                    break;
            }

            if (algorithm == null)
            {
                Console.WriteLine("That algorithm isn't wired up yet.");
                return;
            }

            _lastSolution = algorithm.Solve(_model, _output);
            Console.WriteLine($"Status: {_lastSolution.Status}, Objective: {_lastSolution.ObjectiveValue:0.000}");
        }

        private void SensitivityMenu()
        {
            if (_model is null || _lastSolution is null)
            {
                Console.WriteLine("Solve a model first.");
                return;
            }

            SensitivityAnalysis.Run(_model, _lastSolution, _output);
            Console.WriteLine("Sensitivity analysis performed. Check the output logs.");
            SensitivityAnalysis.InteractiveMenu(_model, _lastSolution, _output);
        }

        private void SaveOutput()
        {
            Console.Write("Path to output file: ");
            var path = Console.ReadLine() ?? "output.txt";
            _output.Save(path);
            Console.WriteLine($"Saved to {path}");
        }
    }
}