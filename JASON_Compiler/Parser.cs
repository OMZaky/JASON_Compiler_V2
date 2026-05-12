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

        List<Token_Class> Datatype_tokens = new List<Token_Class> { Token_Class.Int, Token_Class.Float, Token_Class.String };

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


        // Program -> Function_Statement* Main_Function
        Node Program()
        {
            Node program = new Node("Program");


            if (TokenStream == null || TokenStream.Count == 0)
            {
                Errors.Error_List.Add("Parsing Error: Source code is empty.");
                return program;
            }

            while (InputPointer + 1 < TokenStream.Count &&
                   TokenStream[InputPointer + 1].token_type != Token_Class.Main)
            {
                program.Children.Add(Function_Statement());
            }

            program.Children.Add(Main_Function());

            MessageBox.Show("Parsing Completed Successfully!");
            return program;
        }

        // Main_Function -> Datatype main () Function_Body
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

        // Function_Statement -> Function_Declaration Function_Body
        Node Function_Statement()
        {
            Node funcStmt = new Node("Function_Statement");
            funcStmt.Children.Add(Function_Declaration());
            funcStmt.Children.Add(Function_Body());
            return funcStmt;
        }

        // Function_Declaration -> Datatype FunctionName ( Parameters )
        Node Function_Declaration()
        {
            Node n = new Node("Function_Declaration");
            n.Children.Add(Datatype());

            Node funcName = new Node("FunctionName");
            funcName.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(funcName);

            n.Children.Add(match(Token_Class.LParanthesis));
            n.Children.Add(Parameters());
            n.Children.Add(match(Token_Class.RParanthesis));
            return n;
        }

        // Function_Body -> { Statements Return_Statement }
        Node Function_Body()
        {
            Node n = new Node("Function_Body");
            n.Children.Add(match(Token_Class.LBrace));
            n.Children.Add(Statements());
            n.Children.Add(Return_Statement());
            n.Children.Add(match(Token_Class.RBrace));
            return n;
        }

        Node Parameters()
        {
            Node n = new Node("Parameters");

            if (InputPointer < TokenStream.Count && Datatype_tokens.Contains(TokenStream[InputPointer].token_type))
            {
                n.Children.Add(Parameter());

                while (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Comma)
                {
                    n.Children.Add(match(Token_Class.Comma));
                    n.Children.Add(Parameter());
                }
            }
            return n;
        }

        // Parameter -> Datatype Identifier
        Node Parameter()
        {
            Node n = new Node("Parameter");
            n.Children.Add(Datatype());
            n.Children.Add(match(Token_Class.Identifier));
            return n;
        }


        // There are 3 variations of the nodes Satatements and Statement
        // depending on how we wanna handle the errors


        /* * VARIATION 1: STRICT STARTER PARSING
         * Approach: Only enter the statement loop if the current token is in the 'statementStarters' list.
         * Weakness: Highly fragile. A single unauthorized token (like a stray '*') immediately terminates 
         * the while loop, causing the parser to abandon the rest of the file and miss subsequent errors.
         */

#if false

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
                // In Variation 1, this else block is practically "dead code" because the 
                // statementsNode while loop filters out bad tokens before they ever reach here.
                int lineNum = TokenStream[InputPointer].line_num;
                Errors.Error_List.Add($"Line {lineNum} | Parsing Error: Unexpected token {current} at start of statement.");
                InputPointer++;
            }

            return stmt;
        }

#endif


        /* * VARIATION 2: THE TERMINATOR LOOP (PANIC MODE V1)
         * Approach: Loop continuously until an end-of-block token is found (Return, Until, End, Else).
         * Strength: Guarantees the parser reaches the end of the file instead of quitting on the first typo.
         * Weakness: Causes "Error Cascades". Skipping a single bad token shifts the LL(1) alignment, 
         * causing the parser to misinterpret the next several tokens and spam the error log.
         */



        Node Statements()
        {
            Node statementsNode = new Node("Statements");

            while (InputPointer < TokenStream.Count &&
                   TokenStream[InputPointer].token_type != Token_Class.Return &&
                   TokenStream[InputPointer].token_type != Token_Class.Until &&
                   TokenStream[InputPointer].token_type != Token_Class.End &&
                   TokenStream[InputPointer].token_type != Token_Class.ElseIf &&
                   TokenStream[InputPointer].token_type != Token_Class.Else)
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
                int lineNum = TokenStream[InputPointer].line_num;
                Errors.Error_List.Add($"Line {lineNum} | Parsing Error: Unexpected token {current} at start of statement.\n"); 
                InputPointer++;
            }

            return stmt;
        }



        /* * VARIATION 3: SYNCHRONIZATION TOKEN RECOVERY (OPTIMAL)
         * Approach: Combines the resilient Terminator Loop with a Sync-Token fast-forward mechanism.
         * Strength: Highly robust. When an unauthorized token starts a statement, the parser logs ONE 
         * clean error, then fast-forwards to the nearest semicolon. This perfectly realigns the 
         * Token Stream for the next statement, eliminating Error Cascades entirely.
         */

#if false

        Node Statements()
        {
            Node statementsNode = new Node("Statements");

            // Conveyor Belt: Feed everything into Statement() until a block terminator is reached.
            while (InputPointer < TokenStream.Count &&
                   TokenStream[InputPointer].token_type != Token_Class.Return &&
                   TokenStream[InputPointer].token_type != Token_Class.Until &&
                   TokenStream[InputPointer].token_type != Token_Class.End &&
                   TokenStream[InputPointer].token_type != Token_Class.ElseIf &&
                   TokenStream[InputPointer].token_type != Token_Class.Else)
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

            if (current == Token_Class.Read) stmt.Children.Add(Read_Statement());
            else if (current == Token_Class.Write) stmt.Children.Add(Write_Statement());
            else if (current == Token_Class.If) stmt.Children.Add(If_Statement());
            else if (current == Token_Class.Repeat) stmt.Children.Add(Repeat_Statement());
            else if (Datatype_tokens.Contains(current)) stmt.Children.Add(Declaration_Statement());
            else if (current == Token_Class.Identifier)
            {
                if (InputPointer + 1 < TokenStream.Count && TokenStream[InputPointer + 1].token_type == Token_Class.AssignmentOp)
                    stmt.Children.Add(Assignment_Statement());
                else
                    stmt.Children.Add(Function_Call_Standalone());
            }
            else
            {
                // Panic Mode V2 (Sync Tokens): Log error, then fast-forward to realign.
                int lineNum = TokenStream[InputPointer].line_num;
                Errors.Error_List.Add($"Line {lineNum} | Parsing Error: Unexpected token {current} at start of statement.");

                while (InputPointer < TokenStream.Count &&
                       TokenStream[InputPointer].token_type != Token_Class.Semicolon &&
                       TokenStream[InputPointer].token_type != Token_Class.Return &&
                       TokenStream[InputPointer].token_type != Token_Class.End &&
                       TokenStream[InputPointer].token_type != Token_Class.Until)
                {
                    InputPointer++;
                }

                // Consume the semicolon so the loop starts perfectly fresh on the next valid statement
                if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Semicolon)
                {
                    InputPointer++;
                }
            }

            return stmt;
        }

#endif

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

        Node Assignment_Statement()
        {
            Node n = new Node("Assignment_Statement");
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(match(Token_Class.AssignmentOp));
            n.Children.Add(Expression());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Write_Statement -> write Write_Arg ;
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

        Node Read_Statement()
        {
            Node n = new Node("Read_Statement");
            n.Children.Add(match(Token_Class.Read));
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        Node Return_Statement()
        {
            Node n = new Node("Return_Statement");
            n.Children.Add(match(Token_Class.Return));
            n.Children.Add(Expression());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        
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

        Node Else_Statement()
        {
            Node n = new Node("Else_Statement");
            n.Children.Add(match(Token_Class.Else));
            n.Children.Add(Statements());
            n.Children.Add(match(Token_Class.End));
            return n;
        }

        Node Repeat_Statement()
        {
            Node n = new Node("Repeat_Statement");
            n.Children.Add(match(Token_Class.Repeat));
            n.Children.Add(Statements());
            n.Children.Add(match(Token_Class.Until));
            n.Children.Add(Condition_Statement());
            return n;
        }

       
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

        // Condition -> Identifier Condition_Operator Term
        Node Condition()
        {
            Node n = new Node("Condition");
            n.Children.Add(match(Token_Class.Identifier));
            n.Children.Add(Condition_Operator());
            n.Children.Add(Term());
            return n;
        }

        // Condition_Operator -> < | > | = | <> | <= | >=
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
            n.Children.Add(match(Token_Class.EqualOp));
            return n;
        }


        // Expression -> StringLiteral | Equation
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



        // Term -> Number | Identifier | Function_Call
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
                    // If identifier is followed by '(', it is a Function_Call
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
                    n.Children.Add(match(Token_Class.Number));
                }
            }
            else
            {
                n.Children.Add(match(Token_Class.Number));
            }
            return n;
        }

        // Datatype -> int | float | string
        Node Datatype()
        {
            Node datatype = new Node("Datatype");
            if (InputPointer < TokenStream.Count && Datatype_tokens.Contains(TokenStream[InputPointer].token_type))
            {
                datatype.Children.Add(match(TokenStream[InputPointer].token_type));
            }
            else
            {
                datatype.Children.Add(match(Token_Class.Int));
            }
            return datatype;
        }

        // Function_Call_Standalone -> Function_Call ;
        Node Function_Call_Standalone()
        {
            Node n = new Node("Function_Call_Standalone");
            n.Children.Add(Function_Call());
            n.Children.Add(match(Token_Class.Semicolon));
            return n;
        }

        // Function_Call -> Identifier ( Arguments )
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

            if (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type != Token_Class.RParanthesis)
            {
                n.Children.Add(Expression());

                while (InputPointer < TokenStream.Count && TokenStream[InputPointer].token_type == Token_Class.Comma)
                {
                    n.Children.Add(match(Token_Class.Comma));
                    n.Children.Add(Expression());
                }
            }
            return n;
        }

        

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
                    int lineNum = TokenStream[InputPointer].line_num;

                    Errors.Error_List.Add($"Line {lineNum} | Parsing Error: Expected {ExpectedToken} but found {TokenStream[InputPointer].token_type}.\r\n");

                    InputPointer++;
                    return null;
                }
            }
            else
            {
                string lineInfo = TokenStream.Count > 0 ? $"Line {TokenStream[TokenStream.Count - 1].line_num} | " : "";

                Errors.Error_List.Add($"{lineInfo}Parsing Error: Expected {ExpectedToken} but found EOF\r\n");

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