using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace JASON_Compiler
{
    public class Node
    {
        public List<Node> Children = new List<Node>();
        public string Name;

        public Node(string N)
        {
            this.Name = N;
        }
    }

    public class Parser
    {
        int InputPointer = 0;
        List<Token> TokenStream;
        public Node root;

        // Useful reference lists for routing
        List<Token_Class> Datatype_tokens = new List<Token_Class> { Token_Class.Int, Token_Class.Float, Token_Class.String };
        List<Token_Class> statementStarters = new List<Token_Class>
        {
            Token_Class.Int, Token_Class.Float, Token_Class.String,
            Token_Class.Read, Token_Class.Write,
            Token_Class.Repeat, Token_Class.If,
            Token_Class.Identifier
        };

        List<Token_Class> Arithmetic_Operators = new List<Token_Class>
        {
            Token_Class.PlusOp, Token_Class.MinusOp,
            Token_Class.MultiplyOp, Token_Class.DivideOp
        };

        public Node StartParsing(List<Token> TokenStream)
        {
            this.InputPointer = 0;
            this.TokenStream = TokenStream;
            root = Program();
            return root;
        }

        // =================================================================================
        // THE HIGH LEVEL PROGRAM BLOCKS
        // =================================================================================

        // Rule 31: Program -> Function_Statement* Main_Function
        Node Program()
        {
            Node program = new Node("Program");


            if (TokenStream == null || TokenStream.Count == 0)
            {
                Errors.Error_List.Add("Parsing Error: Source code is empty.");
                return program;
            }

            // Lookahead: Keep looping as long as the token AFTER the datatype is NOT 'main'
            while (InputPointer + 1 < TokenStream.Count &&
                   TokenStream[InputPointer + 1].token_type != Token_Class.Main)
            {
                program.Children.Add(Function_Statement());
            }

            // Once the loop breaks, the only thing left must be the Main Function
            program.Children.Add(Main_Function());

            MessageBox.Show("Parsing Completed Successfully!");
            return program;
        }

        // Rule 30: Main_Function -> Datatype main () Function_Body
        Node Main_Function()
        {
            Node mainFunc = new Node("Main_Function");
            mainFunc.Children.Add(Datatype());
            mainFunc.Children.Add(match(Token_Class.Main));
            mainFunc.Children.Add(match(Token_Class.LParanthesis));
            mainFunc.Children.Add(match(Token_Class.RParanthesis));
            mainFunc.Children.Add(Function_Body());
            return mainFunc;
        }

        // Rule 29: Function_Statement -> Function_Declaration Function_Body
        Node Function_Statement()
        {
            Node funcStmt = new Node("Function_Statement");
            funcStmt.Children.Add(Function_Declaration());
            funcStmt.Children.Add(Function_Body());
            return funcStmt;
        }

        // Rule 27: Function_Declaration -> Datatype FunctionName ( Parameters )
        Node Function_Declaration()
        {
            Node n = new Node("Function_Declaration");
            n.Children.Add(Datatype());

            // FunctionName is just an Identifier (Rule 25)
            Node funcName = new Node("FunctionName");
            funcName.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(funcName);

            n.Children.Add(match(Token_Class.LParanthesis));
            n.Children.Add(Parameters());
            n.Children.Add(match(Token_Class.RParanthesis));
            return n;
        }

        // Rule 28: Function_Body -> { Statements Return_Statement }
        Node Function_Body()
        {
            Node n = new Node("Function_Body");
            n.Children.Add(match(Token_Class.LBrace));
            n.Children.Add(Statements());
            n.Children.Add(Return_Statement());
            n.Children.Add(match(Token_Class.RBrace));
            return n;
        }

        // Rule 26/27 handling: Zero or more Parameters separated by comma
        Node Parameters()
        {
            Node n = new Node("Parameters");

            // Check if there is at least one parameter (starts with a datatype)
            if (InputPointer < TokenStream.Count && Datatype_tokens.Contains(TokenStream[InputPointer].token_type))
            {
                n.Children.Add(Parameter());

                // Loop for multiple parameters separated by commas
                while (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Comma)
                {
                    n.Children.Add(match(Token_Class.Comma));
                    n.Children.Add(Parameter());
                }
            }
            return n;
        }

        // Rule 26: Parameter -> Datatype Identifier
        Node Parameter()
        {
            Node n = new Node("Parameter");
            n.Children.Add(Datatype());
            n.Children.Add(match(Token_Class.Identifier));
            return n;
        }


        // =================================================================================
        // STATEMENTS & ROUTING
        // =================================================================================

        Node Statements()
        {
            Node statementsNode = new Node("Statements");
            while (InputPointer < TokenStream.Count && statementStarters.Contains(TokenStream[InputPointer].token_type))
            {
                statementsNode.Children.Add(Statement());
            }
            return statementsNode;
        }

        Node Statement()
        {
            Node stmt = new Node("Statement");
            if (InputPointer >= TokenStream.Count) return stmt;

            Token_Class current = TokenStream[InputPointer].token_type;

            if (current == Token_Class.Read)
                stmt.Children.Add(Read_Statement());
            else if (current == Token_Class.Write)
                stmt.Children.Add(Write_Statement());
            else if (current == Token_Class.If)
                stmt.Children.Add(If_Statement());
            else if (current == Token_Class.Repeat)
                stmt.Children.Add(Repeat_Statement());
            else if (Datatype_tokens.Contains(current))
                stmt.Children.Add(Declaration_Statement());
            else if (current == Token_Class.Identifier)
            {
                if (InputPointer + 1 < TokenStream.Count && TokenStream[InputPointer + 1].token_type == Token_Class.AssignmentOp)
                    stmt.Children.Add(Assignment_Statement());
                else
                    stmt.Children.Add(Function_Call_Standalone());
            }
            else
            {
                Errors.Error_List.Add("Parsing Error: Unexpected token " + current.ToString() + " at start of statement.");
                InputPointer++;
            }

            return stmt;
        }

        // Rule 13: Declaration_Statement (Handles inline assignments and commas)
        Node Declaration_Statement()
        {
            Node n = new Node("Declaration_Statement");
            n.Children.Add(Datatype());
            n.Children.Add(match(Token_Class.Identifier));

            // Optional assignment: int x := 5;
            if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.AssignmentOp)
            {
                n.Children.Add(match(Token_Class.AssignmentOp));
                n.Children.Add(Expression());
            }

            // Loop for multiple declarations: int x, y := 2, z;
            while (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Comma)
            {
                n.Children.Add(match(Token_Class.Comma));
                n.Children.Add(match(Token_Class.Identifier));

                if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.AssignmentOp)
                {
                    n.Children.Add(match(Token_Class.AssignmentOp));
                    n.Children.Add(Expression());
                }
            }
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Rule 11: Assignment_Statement
        Node Assignment_Statement()
        {
            Node n = new Node("Assignment_Statement");
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(match(Token_Class.AssignmentOp));
            n.Children.Add(Expression());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Rule 14: Write_Statement -> write Write_Arg ;
        Node Write_Statement()
        {
            Node n = new Node("Write_Statement");
            n.Children.Add(match(Token_Class.Write));
            n.Children.Add(Write_Arg());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Write_Arg -> Expression | endl
        Node Write_Arg()
        {
            Node n = new Node("Write_Arg");
            if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Endl)
            {
                n.Children.Add(match(Token_Class.Endl));
            }
            else
            {
                n.Children.Add(Expression());
            }
            return n;
        }

        // Rule 15: Read_Statement
        Node Read_Statement()
        {
            Node n = new Node("Read_Statement");
            n.Children.Add(match(Token_Class.Read));
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Rule 16: Return_Statement
        Node Return_Statement()
        {
            Node n = new Node("Return_Statement");
            n.Children.Add(match(Token_Class.Return));
            n.Children.Add(Expression());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // =================================================================================
        // CONTROL FLOW (IF & REPEAT)
        // =================================================================================

        // Rule 21: If_Statement
        Node If_Statement()
        {
            Node n = new Node("If_Statement");
            n.Children.Add(match(Token_Class.If));
            n.Children.Add(Condition_Statement());
            n.Children.Add(match(Token_Class.Then));
            n.Children.Add(Statements());

            if (InputPointer < TokenStream.Count)
            {
                if (TokenStream[InputPointer].token_type == Token_Class.ElseIf)
                    n.Children.Add(Else_If_Statement());
                else if (TokenStream[InputPointer].token_type == Token_Class.Else)
                    n.Children.Add(Else_Statement());
                else
                    n.Children.Add(match(Token_Class.End));
            }
            return n;
        }

        // Rule 22: Else_If_Statement
        Node Else_If_Statement()
        {
            Node n = new Node("Else_If_Statement");
            n.Children.Add(match(Token_Class.ElseIf));
            n.Children.Add(Condition_Statement());
            n.Children.Add(match(Token_Class.Then));
            n.Children.Add(Statements());

            if (InputPointer < TokenStream.Count)
            {
                if (TokenStream[InputPointer].token_type == Token_Class.ElseIf)
                    n.Children.Add(Else_If_Statement());
                else if (TokenStream[InputPointer].token_type == Token_Class.Else)
                    n.Children.Add(Else_Statement());
                else
                    n.Children.Add(match(Token_Class.End));
            }
            return n;
        }

        // Rule 23: Else_Statement
        Node Else_Statement()
        {
            Node n = new Node("Else_Statement");
            n.Children.Add(match(Token_Class.Else));
            n.Children.Add(Statements());
            n.Children.Add(match(Token_Class.End));
            return n;
        }

        // Rule 24: Repeat_Statement
        Node Repeat_Statement()
        {
            Node n = new Node("Repeat_Statement");
            n.Children.Add(match(Token_Class.Repeat));
            n.Children.Add(Statements());
            n.Children.Add(match(Token_Class.Until));
            n.Children.Add(Condition_Statement());
            return n;
        }

        // =================================================================================
        // CONDITIONS & MATH EXPRESSIONS
        // =================================================================================

        // Rule 20: Condition_Statement (Handles AND/OR looping)
        Node Condition_Statement()
        {
            Node n = new Node("Condition_Statement");
            n.Children.Add(Condition());

            while (InputPointer < TokenStream.Count &&
                  (TokenStream[InputPointer].token_type == Token_Class.AndOp ||
                   TokenStream[InputPointer].token_type == Token_Class.OrOp))
            {
                n.Children.Add(match(TokenStream[InputPointer].token_type)); // Match && or ||
                n.Children.Add(Condition());
            }
            return n;
        }

        // Rule 18: Condition -> Identifier Condition_Operator Term
        Node Condition()
        {
            Node n = new Node("Condition");
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(Condition_Operator());
            n.Children.Add(Term());
            return n;
        }

        // Rule 17: Condition_Operator -> < | > | = | <> | <= | >=
        Node Condition_Operator()
        {
            Node n = new Node("Condition_Operator");
            if (InputPointer < TokenStream.Count)
            {
                Token_Class op = TokenStream[InputPointer].token_type;
                if (op == Token_Class.LessThanOp || op == Token_Class.GreaterThanOp ||
                    op == Token_Class.EqualOp || op == Token_Class.NotEqualOp ||
                    op == Token_Class.LessThanOrEqualOp || op == Token_Class.GreaterThanOrEqualOp)
                {
                    n.Children.Add(match(op));
                    return n;
                }
            }
            // If it reaches here, force an error by expecting an EqualOp
            n.Children.Add(match(Token_Class.EqualOp));
            return n;
        }


        // Rule 10: Expression -> StringLiteral | Equation
        Node Expression()
        {
            Node n = new Node("Expression");
            if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.StringLiteral)
            {
                n.Children.Add(match(Token_Class.StringLiteral));
            }
            else
            {
                n.Children.Add(Equation());
            }
            return n;
        }

        // Equation -> Base_Equation Equation_Tail
        Node Equation()
        {
            Node firstBase = Base_Equation();

            Node tail = Equation_Tail();

            if (tail != null)
            {
                Node eqNode = new Node("Equation");
                eqNode.Children.Add(firstBase);
                eqNode.Children.Add(tail);
                return eqNode;
            }

            return firstBase;
        }

        // Base_Equation -> ( Equation ) | Term
        Node Base_Equation()
        {
            Node n = new Node("Base_Equation");

            if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.LParanthesis)
            {
                n.Children.Add(match(Token_Class.LParanthesis));
                n.Children.Add(Equation());
                n.Children.Add(match(Token_Class.RParanthesis));
            }
            else
            {
                n.Children.Add(Term());
            }
            return n;
        }

        // Equation_Tail -> Arithmetic_Operator Base_Equation Equation_Tail | epsilon
        Node Equation_Tail()
        {
            if (InputPointer < TokenStream.Count &&
                Arithmetic_Operators.Contains(TokenStream[InputPointer].token_type))
            {
                Node n = new Node("Equation_Tail");

                n.Children.Add(match(TokenStream[InputPointer].token_type));

                n.Children.Add(Base_Equation());

                Node nextTail = Equation_Tail();
                if (nextTail != null) n.Children.Add(nextTail);

                return n;
            }

            return null;
        }



        // Rule 7: Term -> Number | Identifier | Function_Call
        Node Term()
        {
            Node n = new Node("Term");
            if (InputPointer < TokenStream.Count)
            {
                if (TokenStream[InputPointer].token_type == Token_Class.Number)
                {
                    n.Children.Add(match(Token_Class.Number));
                }
                else if (TokenStream[InputPointer].token_type == Token_Class.Identifier)
                {
                    // Lookahead: If identifier is followed by '(', it is a Function_Call
                    if (InputPointer + 1 < TokenStream.Count && TokenStream[InputPointer + 1].token_type == Token_Class.LParanthesis)
                    {
                        n.Children.Add(Function_Call());
                    }
                    else
                    {
                        n.Children.Add(match(Token_Class.Identifier));
                    }
                }
                else
                {
                    // Unauthorized: Force error and move pointer
                    n.Children.Add(match(Token_Class.Number));
                }
            }
            else
            {
                n.Children.Add(match(Token_Class.Number)); // EOF error
            }
            return n;
        }

        // Rule 12: Datatype -> int | float | string
        Node Datatype()
        {
            Node datatype = new Node("Datatype");
            if (InputPointer < TokenStream.Count && Datatype_tokens.Contains(TokenStream[InputPointer].token_type))
            {
                datatype.Children.Add(match(TokenStream[InputPointer].token_type));
            }
            else
            {
                // Unauthorized: Expect a datatype token to trigger error logging
                datatype.Children.Add(match(Token_Class.Int));
            }
            return datatype;
        }

        // Rule: Function_Call_Standalone -> Function_Call ;
        Node Function_Call_Standalone()
        {
            Node n = new Node("Function_Call_Standalone");
            n.Children.Add(Function_Call());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Rule 6: Function_Call -> Identifier ( Arguments )
        Node Function_Call()
        {
            Node n = new Node("Function_Call");
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(match(Token_Class.LParanthesis));
            n.Children.Add(Arguments());
            n.Children.Add(match(Token_Class.RParanthesis));
            return n;
        }

        // Arguments -> Expression Argument_Tail | ε
        Node Arguments()
        {
            Node n = new Node("Arguments");

            // Check if there are arguments inside (not a right parenthesis)
            if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type != Token_Class.RParanthesis)
            {
                n.Children.Add(Expression());

                // Loop for multiple arguments separated by commas
                while (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Comma)
                {
                    n.Children.Add(match(Token_Class.Comma));
                    n.Children.Add(Expression());
                }
            }
            return n;
        }

        // =================================================================================
        // UTILITIES
        // =================================================================================

        public Node match(Token_Class ExpectedToken)
        {
            if (InputPointer < TokenStream.Count)
            {
                if (ExpectedToken == TokenStream[InputPointer].token_type)
                {
                    Node newNode = new Node(TokenStream[InputPointer].lex);
                    InputPointer++;
                    return newNode;
                }
                else
                {
                    Errors.Error_List.Add("Parsing Error: Expected "
                        + ExpectedToken.ToString() + " but found " +
                        TokenStream[InputPointer].token_type.ToString() + ".\r\n");
                    InputPointer++;
                    return null;
                }
            }
            else
            {
                Errors.Error_List.Add("Parsing Error: Expected " + ExpectedToken.ToString() + " but found EOF\r\n");
                return null;
            }
        }

        public static TreeNode PrintParseTree(Node root)
        {
            TreeNode tree = new TreeNode("Parse Tree");
            TreeNode treeRoot = PrintTree(root);
            if (treeRoot != null)
                tree.Nodes.Add(treeRoot);
            return tree;
        }

        static TreeNode PrintTree(Node root)
        {
            if (root == null || root.Name == null)
                return null;

            TreeNode tree = new TreeNode(root.Name);
            if (root.Children.Count == 0)
                return tree;

            foreach (Node child in root.Children)
            {
                if (child == null)
                    continue;
                tree.Nodes.Add(PrintTree(child));
            }
            return tree;
        }
    }
}