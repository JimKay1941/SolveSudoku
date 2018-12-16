using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace UserInterface
{
    public partial class Form1 : Form
    {
        // Saved location: C:\Users\jkay\Saved Games\Sudoku\ChinaPost001.csv
        // file browse processes
        private readonly OpenFileDialog chooseInputFileDialog = new OpenFileDialog();
        private readonly OpenFileDialog chooseOutputFileDialog = new OpenFileDialog();
        // end file browse

        int[, ,] Values = new int[9, 9, 10]; //column, row, floor (Be careful!)
        String[] GameData = new String[9]; // an array of pointers where I can read in a game file (a csv with starting values)
        TextBox[,] GameText = new TextBox[9, 9]; // an array of pointers so I can walk through with loops

        Int16[, , ,] BoxLimits = new Int16[9, 9, 2, 2]; // holds the limits of the 3x3 box holding any square

        bool ValidBoard = true;

        int Depth = 0;
        int DepthLimit = 1;

        Int16 Solved = new Int16(); // holds the five counters for finding out when a sub-game has been completed
        Int16 Guesses = new Int16(); // tell how many trial runs were needed to reach a solution
        Int16 AloneStack = new Int16(); // tell how many squares solved by "alone in stack"
        Int16 AloneColRow = new Int16(); // tell how many squares solved by "alone in column" or "alone in row"
        Int16 AloneBox = new Int16(); // tell how many squares solved by "alone in box"
        Int16 Difficulty = new Int16(); // calculate difficulty level as 0 + 18*Guesses + 4*box + 3*ColRow + 2*Stack - 1*Given
        Int16 Given = 0; // tell how many squares were given in the initial game

        bool changed = new bool(); // tells us if a pass through the five sub-game boards made any progress
        bool DeadEnd = false;

        public class MayBe
        {
            public Int16 col;
            public Int16 row;
            public Int16 floor;
            public Int16 AloneStack;
            public Int16 AloneColRow;
            public Int16 AloneBox;

            public MayBe(Int16 Col, Int16 Row, Int16 Floor)
            {
                col = Col;
                row = Row;
                floor = Floor;
                AloneStack = 0;
                AloneColRow = 0;
                AloneBox = 0;
            }
        }

        public Form1()
        {
            InitializeComponent();
            // my file dialogs
            chooseInputFileDialog.FileOk += OnInputFileDialogOK;
            chooseOutputFileDialog.FileOk += OnOutputFileDialogOK;
        }

        private void SpecialBackgroundColors()
        {
            this.textBox00.BackColor = System.Drawing.Color.Red;
            this.textBox01.BackColor = System.Drawing.Color.Red;
            this.textBox02.BackColor = System.Drawing.Color.Red;
            this.textBox06.BackColor = System.Drawing.Color.Red;
            this.textBox07.BackColor = System.Drawing.Color.Red;
            this.textBox08.BackColor = System.Drawing.Color.Red;
            this.textBox10.BackColor = System.Drawing.Color.Red;
            this.textBox11.BackColor = System.Drawing.Color.Red;
            this.textBox12.BackColor = System.Drawing.Color.Red;
            this.textBox16.BackColor = System.Drawing.Color.Red;
            this.textBox17.BackColor = System.Drawing.Color.Red;
            this.textBox18.BackColor = System.Drawing.Color.Red;
            this.textBox20.BackColor = System.Drawing.Color.Red;
            this.textBox21.BackColor = System.Drawing.Color.Red;
            this.textBox22.BackColor = System.Drawing.Color.Red;
            this.textBox26.BackColor = System.Drawing.Color.Red;
            this.textBox27.BackColor = System.Drawing.Color.Red;
            this.textBox28.BackColor = System.Drawing.Color.Red;
            this.textBox33.BackColor = System.Drawing.Color.Red;
            this.textBox34.BackColor = System.Drawing.Color.Red;
            this.textBox35.BackColor = System.Drawing.Color.Red;
            this.textBox43.BackColor = System.Drawing.Color.Red;
            this.textBox44.BackColor = System.Drawing.Color.Red;
            this.textBox45.BackColor = System.Drawing.Color.Red;
            this.textBox53.BackColor = System.Drawing.Color.Red;
            this.textBox54.BackColor = System.Drawing.Color.Red;
            this.textBox55.BackColor = System.Drawing.Color.Red;
            this.textBox60.BackColor = System.Drawing.Color.Red;
            this.textBox61.BackColor = System.Drawing.Color.Red;
            this.textBox62.BackColor = System.Drawing.Color.Red;
            this.textBox66.BackColor = System.Drawing.Color.Red;
            this.textBox67.BackColor = System.Drawing.Color.Red;
            this.textBox68.BackColor = System.Drawing.Color.Red;
            this.textBox70.BackColor = System.Drawing.Color.Red;
            this.textBox71.BackColor = System.Drawing.Color.Red;
            this.textBox72.BackColor = System.Drawing.Color.Red;
            this.textBox76.BackColor = System.Drawing.Color.Red;
            this.textBox77.BackColor = System.Drawing.Color.Red;
            this.textBox78.BackColor = System.Drawing.Color.Red;
            this.textBox80.BackColor = System.Drawing.Color.Red;
            this.textBox81.BackColor = System.Drawing.Color.Red;
            this.textBox82.BackColor = System.Drawing.Color.Red;
            this.textBox86.BackColor = System.Drawing.Color.Red;
            this.textBox87.BackColor = System.Drawing.Color.Red;
            this.textBox88.BackColor = System.Drawing.Color.Red;
        }

        private void Solve_Click(object sender, EventArgs e)
        {
            Say.Text = "";
            if (DepthLim.Text != "")
                DepthLimit = Convert.ToInt32(DepthLim.Text);
            // validation moved to procedure
            ValidBoard = true;
            if (Given == 0)
            {
                this.Say.Text = "No initial game entered!";
                return;
            }
            ValidateBoard_Click(sender, e);
            if (!ValidBoard) return;

            // The intput has been validated as logically consistent and the bottom layer of the cube has been set to the
            //input values. Now we have to set the upper layers.

            // There are two phases to generating a solution. First, every game board cell containing a non-zero value had to
            // have exclusive rights to that value. This is done by three actions:
            //  1. clear all of the floors above that cell to indicate there are no other values that can be put there.
            //  2. on the floor corresponding to the actual value, clear the row and column so no conflicts will be created.
            //  3. on the same floor as 2, clear the containing 3 x 3 sub-square so that no conflict of that kind can be created.
            //
            // This proces is exactly the same as what happens when a value is taken from above a cell and put in as the solution.
            // The same three sub-routines will be use in both sections.

            // Floor 0 has the game board values input by the user.
            for (int row = 0; row <= 8; row++)
            {
                for (int col = 0; col <= 8; col++)
                {
                    // check each floor 0 cell for a non-zero value
                    if (Values[col, row, 0] != 0)
                    {
                        // zero out the entire column above a filled cell on floor 0
                        zero_the_floors(col, row);

                        // using the current value as the floor number, do the rest of the work there
                        zero_the_row(col, row, Values[col, row, 0]);

                        // using the current value as the floor number, zero the column
                        zero_the_col(col, row, Values[col, row, 0]);

                        // finally, zero the containing sub-box
                        zero_this_box(col, row, Values[col, row, 0]);
                    }
                }
            }

            // at this point, we have assured ourselves that the user has entered a logically consistent game board
            // every box has a number in the range of 0-9 and our job is to fill in the zero boxes still on floor 0.
            // using the values in the bottom layer, we have initialized the upper layers so all we need to do is iterate
            // until we have a unique solution or we iterate and find nothing to change but still don't have a solution

            // at this point we have finished the solution logic and have returned either a solution or an indication that
            // the input board does not have a unique solution

            // define the progress indicator

            // create an infinite loop that ends when there is no further progress but the game is not completed
            changed = false;
            do
            {
                if (Solved == 81) break;
                do
                {
                    if (Solved == 81) break; ; //this means the game is over
                    // set the progress indicator
                    changed = false;
                    // the first pass looks at the column of possibilities over each board square. If there is just one
                    // item in the floor stack, then we apply that to level 0, which will put it on the board. Once a value is used,
                    // it must be removed from the floor stack AND from its row and column as well. AND! Remove from it's box too!
                    for (int onerow = 0; onerow < 9; onerow++)
                    {
                        for (int onecol = 0; onecol < 9; onecol++)
                        {
                            // check each floor 0 cell for a zero value
                            if (Values[onecol, onerow, 0] == 0)
                            {
                                // count the possible values in the column above
                                if (count_above(onecol, onerow) == 1)
                                {
                                    int CheckOnly = get_only_above(onecol, onerow);
                                    if (CheckOnly != 0)
                                    {
                                        Values[onecol, onerow, 0] = CheckOnly; // alone in stack
                                        if (Stepping.Checked == true)
                                        {
                                            GameText[onecol, onerow].Text = CheckOnly.ToString();
                                            GameText[onecol, onerow].BackColor = System.Drawing.SystemColors.Highlight;
                                            Say.Text = "Alone in Stack";
                                            AloneStack++;
                                            return;
                                        }
                                        Solved++;
                                        AloneStack++;
                                        changed = true;
                                        // zero out the entire column above a filled cell on floor 0
                                        zero_the_floors(onecol, onerow);

                                        // using the current value as the floor number, do the rest of the work there
                                        zero_the_row(onecol, onerow, Values[onecol, onerow, 0]);

                                        // using the current value as the floor number, zero the column
                                        zero_the_col(onecol, onerow, Values[onecol, onerow, 0]);

                                        // finally, zero the containing sub-box
                                        zero_this_box(onecol, onerow, Values[onecol, onerow, 0]);
                                    }
                                }
                            }
                        }
                    }
                    //} while (changed);
                    // the above may not produce a solution because the following can arise
                    // there are, for example, two possible values over one board square. One of them can only be
                    // used in this square but the other can be used elsewhere as well.
                    // the desired action is to use the 'here only' number but the strategy above cannot identify
                    // this situation. So a second strategy is needed.

                    // this second strategy examines each 'floor' above the board, starting from [1]. If a number is found to be
                    // alone in it's row, OR column, OR sub-square, it will be applied to the solution and removed from the same.
                    //strat 2 start
                    // debugging
                    //do
                    //{
                    changed = false;
                    if (Solved == 81) break; // in this second loop, if every game box is full, we have solve the soduko

                    for (int col = 0; col <= 8; col++)
                    {
                        for (int row = 0; row <= 8; row++)
                        {
                            for (int floor = 1; floor <= 9; floor++)
                            {
                                // check each floor cell for a non-zero value
                                // check to see if this box is unsolved
                                if ((Values[col, row, 0] == 0) && (Values[col, row, floor] != 0))
                                {
                                    int row_find2 = 0;
                                    // count the possibles of this values in the current row
                                    for (int index = 0; index < 9; index++)
                                        if (Values[col, index, floor] != 0)
                                        {
                                            row_find2++;
                                        }
                                    // if there is a possible value alone in this row AND it's over the game box we are evaluating
                                    if (row_find2 == 1)
                                    // this means we have a value alone in its row, located at row, column, floor
                                    {
                                        // clear the stack
                                        changed = true;
                                        //Work
                                        Values[col, row, 0] = Values[col, row, floor];  // alone in row
                                        if (Stepping.Checked == true)
                                        {
                                            GameText[col, row].Text = Convert.ToString(Values[col, row, 0]);
                                            GameText[col, row].BackColor = System.Drawing.SystemColors.Highlight;
                                            Say.Text = "Alone in Row";
                                            AloneColRow++;
                                            return;
                                        }
                                        Solved++;
                                        AloneColRow++;
                                        // zero out the entire column above a filled cell on floor 0
                                        zero_the_floors(col, row);

                                        // using the current value as the floor number, do the rest of the work there
                                        zero_the_row(col, row, Values[col, row, 0]);

                                        // using the current value as the floor number, zero the column
                                        zero_the_col(col, row, Values[col, row, 0]);

                                        // finally, zero the containing sub-box
                                        zero_this_box(col, row, Values[col, row, 0]);
                                    }
                                    else
                                    {
                                        // if it wasn't alone in its row, check the column
                                        // count the possibles of this values in the current column
                                        int col_find2 = 0;
                                        int index;
                                        for (index = 0; index < 9; index++)
                                        {
                                            if (Values[index, row, floor] != 0)
                                            {
                                                col_find2++;
                                            }
                                        }
                                        if (col_find2 == 1)
                                        // this means we have a value alone in its row AND it's for the current game box
                                        {
                                            // clear the stack

                                            changed = true;
                                            int tempB = Values[col, row, floor];
                                            Values[col, row, 0] = Values[col, row, floor];  // alone in col
                                            if (Stepping.Checked == true)
                                            {
                                                GameText[col, row].Text = Convert.ToString(Values[col, row, 0]);
                                                GameText[col, row].BackColor = System.Drawing.SystemColors.Highlight;
                                                Say.Text = "Alone in Column";
                                                AloneColRow++;
                                                return;
                                            }
                                            Solved++;
                                            AloneColRow++;
                                            // zero out the entire column above a filled cell on floor 0
                                            zero_the_floors(col, row);

                                            // using the current value as the floor number, do the rest of the work there
                                            zero_the_row(col, row, Values[col, row, 0]);

                                            // using the current value as the floor number, zero the column
                                            zero_the_col(col, row, Values[col, row, 0]);

                                            // finally, zero the containing sub-box
                                            zero_this_box(col, row, Values[col, row, 0]);
                                        }
                                        else
                                        {
                                            // if it wasn't alone in its row OR column, check the box
                                            int box_find2 = 0;
                                            int base_row = (row / 3) * 3;
                                            int base_col = (col / 3) * 3;
                                            int[] val = new int[9];
                                            int box_index = 0;

                                            // a box is an inner 3x3 section of the board
                                            // we take this inner box and string it out into a 1D array
                                            // then we use our standard logic to check for a lone value
                                            for (int w1 = base_col; w1 < base_col + 3; w1++)
                                            {
                                                for (int w2 = base_row; w2 < base_row + 3; w2++)
                                                {
                                                    val[box_index] = Values[w1, w2, floor];
                                                    box_index++;
                                                }
                                            }
                                            // at this point, the 1D array val contains the values from a single box
                                            box_find2 = 0;
                                            for (int x1 = 0; x1 <= 8; x1++)
                                            {
                                                int temp2 = val[x1];
                                                if (val[x1] != 0)
                                                    box_find2++;
                                            }

                                            if ((box_find2 == 1) && (Values[col, row, floor] != 0))
                                            {
                                                // this value is alone it its box so go ahead and use it
                                                changed = true;
                                                if ((col == 0) && (row == 6))
                                                {
                                                    changed = true;
                                                }
                                                Values[col, row, 0] = Values[col, row, floor];  // alone in box
                                                if (Stepping.Checked == true)
                                                {
                                                    GameText[col, row].Text = Convert.ToString(Values[col, row, 0]);
                                                    GameText[col, row].BackColor = System.Drawing.SystemColors.Highlight;
                                                    Say.Text = "Alone in Box";
                                                    AloneBox++;
                                                    return;
                                                }
                                                Solved++;
                                                AloneBox++;
                                                // zero out the entire column above a filled cell on floor 0
                                                zero_the_floors(col, row);

                                                // using the current value as the floor number, do the rest of the work there
                                                zero_the_row(col, row, Values[col, row, 0]);

                                                // using the current value as the floor number, zero the column
                                                zero_the_col(col, row, Values[col, row, 0]);

                                                // finally, zero the containing sub-box
                                                zero_this_box(col, row, Values[col, row, 0]);
                                            }
                                        }
                                    }
                                }
                                if (Solved == 81)
                                    break;
                            }
                            if (Solved == 81)
                                break;
                        }
                        if (Solved == 81)
                            break;
                    }
                } while (changed);
            } while ((changed) && (Solved <= 81));

            // well, that's all folks. we have a unique solution or we don't.
            // go back to the caller in case guesses are being made

            if (Solved != 81)
            {
                this.Say.Text = "There is no unique solution to this board.";
                PostTheScores();
            }

            if (Solved < 81)
            {
                // one last check to see if the Game was a Deadend.
                for (int col = 0; col <= 8; col++)
                {
                    for (int row = 0; row <= 8; row++)
                    {
                        if (Values[col, row, 0] == 0)
                        {
                            if (count_above(col, row) == 0)
                            {
                                DeadEnd = true;
                            }
                        }
                    }
                }
            }
            // Post the game results
            PostTheScores();
            // time to go home.
        }

        private void PostTheScores()
        {
            Solved = 0;
            // happily we found a better way
            for (int y = 0; y <= 8; y++)
            {
                for (int x = 0; x <= 8; x++)
                {
                    GameText[x, y].Text = "";

                    if ((Values[x, y, 0] != 0))
                    {
                        GameText[x, y].Text = Convert.ToString(Values[x, y, 0]);
                        Solved++;
                    }
                }
            }
            // Post the game scores
            NoSolved.Text = Convert.ToString(Solved - Given);
            textGiven.Text = Convert.ToString(Given);

            // Post the guesses used
            NoGuessed.Text = Convert.ToString(Guesses);
            textAloneColRow.Text = Convert.ToString(AloneColRow);
            textAloneStack.Text = Convert.ToString(AloneStack);
            textAloneBox.Text = Convert.ToString(AloneBox);
            textDifficulty.Text = Convert.ToString(18 * Guesses + 4 * AloneBox + 3 * AloneColRow + 2 * AloneStack - 1 * Given);
            if ((DeadEnd) && (Solved != 81))
                this.Say.Text = "Dead end game.";

            if ((!DeadEnd) && (Solved != 81))
                this.Say.Text = "Sorry, no single guess can solve this puzzle!";

            if (Solved == 81)
                this.Say.Text = "Good Show!";

            // time to go home.
        }

        private int count_above(int col, int row)
        {
            int NonZeros = 0;

            for (int z1 = 1; z1 < 10; z1++)
            {
                if (Values[col, row, z1] != 0)
                {
                    NonZeros++;
                }
            }
            return NonZeros;
        }

        private int get_only_above(int col, int row)
        {
            if (count_above(col, row) != 1)
                return 0;

            for (int z1 = 1; z1 < 10; z1++)
            {
                if (Values[col, row, z1] != 0)
                {
                    return Values[col, row, z1];
                }
            }
            return 0;
        }

        private void zero_the_row(int col, int row, int floor)
        {
            for (int y1 = 0; y1 < 9; y1++)
            {
                Values[y1, row, floor] = 0;
            }
        }

        private void zero_the_col(int col, int row, int floor)
        {
            for (int y1 = 0; y1 < 9; y1++)
            {
                Values[col, y1, floor] = 0;
            }
        }

        private void zero_the_floors(int col, int row)
        {
            for (int floor = 1; floor < 10; floor++)
            {
                Values[col, row, floor] = 0;
            }
        }

        private void zero_this_box(int Col, int Row, int floor)
        {
            int base_row = (Row / 3) * 3;
            int base_col = (Col / 3) * 3;

            for (int col = base_col; col < base_col + 3; col++)
            {
                for (int row = base_row; row < base_row + 3; row++)
                {
                    Values[col, row, floor] = 0;
                }
            }
        }

        private bool validate_box(int Col, int Row)
        {
            int base_row = (Row / 3) * 3;
            int base_col = (Col / 3) * 3;

            int[] val = new int[9];
            int index = 0;

            // a box is an inner 3x3 section of the board
            // we take this inner box and string it out into a 1D array
            // then we use our standard logic to check for duplicates
            for (int col = base_col; col < base_col + 3; col++)
            {
                if (Say.Text == "logical error in starting values")
                    break;
                for (int row = base_row; row < base_row + 3; row++)
                {
                    val[index] = Values[col, row, 0];
                    index++;
                }
            }
            // at this point, the 1D array val contains the values from a single box
            for (int x1 = 0; x1 < 8; x1++)
            {
                if (Say.Text == "logical error in starting values")
                    break;

                for (int x2 = x1 + 1; x2 < 9; x2++)
                {
                    int val1 = val[x1];
                    int val2 = val[x2];
                    if ((val[x1] == val[x2]) &&
                        (val[x1] != 0) &&
                        (val[x2] != 0))
                    {
                        this.Say.Text = "logical error in starting values";
                        break;
                    }
                }
            }
            if (Say.Text == "logical error in starting values")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool validate_col(int col)
        {
            for (int w1 = 0; w1 < 8; w1++)
            {
                if (Say.Text == "logical error in starting values")
                    break;
                for (int w2 = w1 + 1; w2 < 9; w2++)
                {
                    int val1 = Values[col, w1, 0];
                    int val2 = Values[col, w2, 0];
                    if ((val1 == val2) && (val1 != 0))
                    {
                        this.Say.Text = "logical error in starting values";
                        break;
                    }
                }
            }
            if (Say.Text == "logical error in starting values")
                return false;
            else
                return true;
        }

        private bool validate_row(int row)
        {
            for (int w1 = 0; w1 < 8; w1++)
            {
                if (Say.Text == "logical error in starting values")
                {
                    return false;
                }
                for (int w2 = w1 + 1; w2 < 9; w2++)
                {
                    int val1 = Values[w1, row, 0];
                    int val2 = Values[w2, row, 0];
                    if ((val1 == val2) && (val1 != 0))
                    {
                        this.Say.Text = "logical error in starting values";
                        return false;
                    }
                }
            }
            if (Say.Text == "logical error in starting values")
                return false;
            else
                return true;
        }

        private bool validate_number(string textbox)
        {
            if (textbox.Length == 0)
            {
                return true;
            }
            if (textbox.Length != 1)
            {
                return false;
            }
            else
            {
                if ((textbox[0] == '1') ||
                    (textbox[0] == '2') ||
                    (textbox[0] == '3') ||
                    (textbox[0] == '4') ||
                    (textbox[0] == '5') ||
                    (textbox[0] == '6') ||
                    (textbox[0] == '7') ||
                    (textbox[0] == '8') ||
                    (textbox[0] == '9'))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            GameText[0, 0] = textBox00;
            GameText[0, 1] = textBox01;
            GameText[0, 2] = textBox02;
            GameText[0, 3] = textBox03;
            GameText[0, 4] = textBox04;
            GameText[0, 5] = textBox05;
            GameText[0, 6] = textBox06;
            GameText[0, 7] = textBox07;
            GameText[0, 8] = textBox08;
            GameText[1, 0] = textBox10;
            GameText[1, 1] = textBox11;
            GameText[1, 2] = textBox12;
            GameText[1, 3] = textBox13;
            GameText[1, 4] = textBox14;
            GameText[1, 5] = textBox15;
            GameText[1, 6] = textBox16;
            GameText[1, 7] = textBox17;
            GameText[1, 8] = textBox18;
            GameText[2, 0] = textBox20;
            GameText[2, 1] = textBox21;
            GameText[2, 2] = textBox22;
            GameText[2, 3] = textBox23;
            GameText[2, 4] = textBox24;
            GameText[2, 5] = textBox25;
            GameText[2, 6] = textBox26;
            GameText[2, 7] = textBox27;
            GameText[2, 8] = textBox28;
            GameText[3, 0] = textBox30;
            GameText[3, 1] = textBox31;
            GameText[3, 2] = textBox32;
            GameText[3, 3] = textBox33;
            GameText[3, 4] = textBox34;
            GameText[3, 5] = textBox35;
            GameText[3, 6] = textBox36;
            GameText[3, 7] = textBox37;
            GameText[3, 8] = textBox38;
            GameText[4, 0] = textBox40;
            GameText[4, 1] = textBox41;
            GameText[4, 2] = textBox42;
            GameText[4, 3] = textBox43;
            GameText[4, 4] = textBox44;
            GameText[4, 5] = textBox45;
            GameText[4, 6] = textBox46;
            GameText[4, 7] = textBox47;
            GameText[4, 8] = textBox48;
            GameText[5, 0] = textBox50;
            GameText[5, 1] = textBox51;
            GameText[5, 2] = textBox52;
            GameText[5, 3] = textBox53;
            GameText[5, 4] = textBox54;
            GameText[5, 5] = textBox55;
            GameText[5, 6] = textBox56;
            GameText[5, 7] = textBox57;
            GameText[5, 8] = textBox58;
            GameText[6, 0] = textBox60;
            GameText[6, 1] = textBox61;
            GameText[6, 2] = textBox62;
            GameText[6, 3] = textBox63;
            GameText[6, 4] = textBox64;
            GameText[6, 5] = textBox65;
            GameText[6, 6] = textBox66;
            GameText[6, 7] = textBox67;
            GameText[6, 8] = textBox68;
            GameText[7, 0] = textBox70;
            GameText[7, 1] = textBox71;
            GameText[7, 2] = textBox72;
            GameText[7, 3] = textBox73;
            GameText[7, 4] = textBox74;
            GameText[7, 5] = textBox75;
            GameText[7, 6] = textBox76;
            GameText[7, 7] = textBox77;
            GameText[7, 8] = textBox78;
            GameText[8, 0] = textBox80;
            GameText[8, 1] = textBox81;
            GameText[8, 2] = textBox82;
            GameText[8, 3] = textBox83;
            GameText[8, 4] = textBox84;
            GameText[8, 5] = textBox85;
            GameText[8, 6] = textBox86;
            GameText[8, 7] = textBox87;
            GameText[8, 8] = textBox88;

            // make sure the game board is clear (it should already be clear at load time
            for (int x = 0; x <= 8; x++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    GameText[x, y].Text = "";
                    GameText[x, y].BackColor = System.Drawing.SystemColors.Info;
                }
            }
            SpecialBackgroundColors();
            // setup the Values array
            for (int col = 0; col < 9; col++)
            {
                for (int row = 0; row < 9; row++)
                {
                    for (int floor = 0; floor < 10; floor++)
                        Values[col, row, floor] = floor;
                }
            }
        }

        private void Exit_Click_1(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Clear_Click(object sender, EventArgs e)
        {
            Guesses = 0;
            Solved = 0;
            // clear the game board
            for (int x = 0; x <= 8; x++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    GameText[x, y].Text = "";
                    GameText[x, y].BackColor = System.Drawing.SystemColors.Info;
                }
            }
            SpecialBackgroundColors();
            // clear the Values array
            for (int col = 0; col < 9; col++)
            {
                for (int row = 0; row < 9; row++)
                {
                    for (int floor = 0; floor < 10; floor++)
                        Values[col, row, floor] = floor;
                }
            }
            NoSolved.Text = "";
            NoGuessed.Text = "";
            textGiven.Text = "";
            textAloneColRow.Text = "";
            textAloneStack.Text = "";
            textAloneBox.Text = "";
            textDifficulty.Text = "";
        }

        private void textBoxNN_TextChanged(object sender, EventArgs e)
        {
            if (validate_number(this.textBox88.Text) != true)
            {
                this.textBox88.Text = "";
                this.Say.Text = "invalid entry in box 8,8";
                return;
            }
            else
            {
                this.Say.Text = "";
                return;
            }
        }

        private void ExtractOptions(int[, ,] Values, Stack<MayBe> MayBeS)
        {
            for (Int16 col = 0; col <= 8; col++)
            {
                for (Int16 row = 0; row <= 8; row++)
                {
                    if (Values[col, row, 0] == 0)
                    {
                        for (Int16 floor = 1; floor <= 9; floor++)
                        {
                            if (Values[col, row, floor] != 0)
                            {
                                MayBeS.Push(new MayBe(col, row, floor));
                            }
                        }
                    }
                }
            }
        }

        private void ReadGame_Click(object sender, EventArgs e)
        {
            try
            {
                FileStream Starting = new FileStream(LoadGame.Text, FileMode.Open, FileAccess.Read, FileShare.None);
                StreamReader Lines = new StreamReader(Starting);

                for (int y = 0; y <= 8; y++)
                {
                    GameData[y] = Lines.ReadLine();
                }
                Lines.Close();
                char[] Delim = { ',' };
                // with luck, we have 9 lines of CVS with the data for the game rows.

                Given = 0;
                for (int y = 0; y <= 8; y++)
                {
                    string[] Row = GameData[y].Split(Delim);
                    // put the row data into the text boxes
                    for (int x = 0; x < Row.Length; x++)
                    {
                        GameText[y, x].Text = Row[x];
                        if (Row[x] != "")
                        {
                            Given++;
                        }
                    }
                }
            }
            catch
            {
                Say.Text = "File not found!";
                return;
            }
        }

        private void WriteGame_Click(object sender, EventArgs e)
        {
            string Line1;
            try
            {
                //
                FileInfo Code = new FileInfo(this.SaveGame.Text);
                StreamWriter Lines = Code.CreateText();

                for (Int16 y = 0; y <= 8; y++)
                {
                    Line1 = "";

                    for (Int16 x = 0; x <= 8; x++)
                    {
                        Line1 += GameText[y, x].Text.Substring(0);

                        if (x < 8)
                        {
                            Line1 += ",";
                        }
                        else
                        {
                            Line1 += "\n";
                        }
                    }
                    Lines.Write(Line1);
                }
                Lines.Close();
                Say.Text = "Solution Board Saved";
            }
            catch
            {
                return;
            }
        }

        private void WriteInput_Click(object sender, EventArgs e)
        {
            string Line1;
            try
            {
                //
                FileInfo Code = new FileInfo(this.LoadGame.Text);
                StreamWriter Lines = Code.CreateText();

                for (Int16 y = 0; y <= 8; y++)
                {
                    Line1 = "";

                    for (Int16 x = 0; x <= 8; x++)
                    {
                        Line1 += GameText[y, x].Text.Substring(0);

                        if (x < 8)
                        {
                            Line1 += ",";
                        }
                        else
                        {
                            Line1 += "\n";
                        }
                    }
                    Lines.Write(Line1);
                }
                Lines.Close();
                Say.Text = "Input Board Saved";
            }
            catch
            {
                return;
            }
        }

        private void Solve_Click_1(object sender, EventArgs e)
        {
            Given = 0;
            for (int y = 0; y <= 8; y++)
            {
                for (int x = 0; x <= 8; x++)
                {
                    if (GameText[y, x].Text != "")
                    {
                        Given++;
                    }
                }
            }
            // be sure the board is good before even starting to solve it
            if (Given == 0)
            {
                this.Say.Text = "No initial game entered!";
                return;
            }
            ValidateBoard_Click(sender, e);
            if (!ValidBoard) return;

            // Run the main solution logic and then see if some 'trial and error' processing is needed
            Solve_Click(sender, e);
            if (!ValidBoard) return;
            //Guesses = 0;

            //Start Loop
            if ((Solved < 81) && !DeadEnd)
            {
                // create a local copy of the game board as expanded by the first Solve_Click() call
                // happily we found a better way
                string[,] LocalBoard = new string[9, 9];

                for (int col = 0; col <= 8; col++)
                {
                    for (int row = 0; row <= 8; row++)
                    {
                        LocalBoard[col, row] = GameText[col, row].Text;
                    }
                }
                int[, ,] LocalValues = new int[9, 9, 10]; //column, row, floor (Be careful!)
                for (int col = 0; col <= 8; col++)
                {
                    for (int row = 0; row <= 8; row++)
                    {
                        for (int floor = 0; floor <= 9; floor++)
                        {
                            LocalValues[col, row, floor] = Values[col, row, floor];
                        }
                    }
                }
                HaveAGuess(sender, e, LocalValues, LocalBoard);
            }
            // put the results on the game board so the player can see them
            PostTheScores();
        }

        private void HaveAGuess(object sender, EventArgs e, int[, ,] GuessValues, string[,] GuessBoard)
        {
            // Increase the depth count
            Depth++;

            Stack<MayBe> MayBeS;
            MayBeS = new Stack<MayBe>();
            MayBe TryMayBe = new MayBe(0, 0, 0);
            // First we create a list of the available valid values
            // The List is MaybeS
            // The Items are MayBe
            // Each Item contains:
            //                    x: the x value of an unsolved square
            //                    y: the y value of an unsolved square
            //                 item: one the two or more values still valid in square x,y
            ExtractOptions(GuessValues, MayBeS);

            // This is the 'desperation' logic that used to be in the solve loop
            // This loop is the 'desperation' final try.
            while ((Solved < 81) && (MayBeS.Count > 0))
            {
                // add one guess at a time then see if the game gets solved
                TryMayBe = MayBeS.Pop();
                GameText[TryMayBe.col, TryMayBe.row].Text = Convert.ToString(TryMayBe.floor);
                Guesses++;

                // see if this single guess can solve the current SubGame
                Solve_Click(sender, e);

                if ((Solved < 81) && (Depth < DepthLimit) && !DeadEnd)
                {
                    int[, ,] LocalValues = new int[9, 9, 10]; //column, row, floor (Be careful!)
                    for (int col = 0; col <= 8; col++)
                    {
                        for (int row = 0; row <= 8; row++)
                        {
                            for (int floor = 0; floor <= 9; floor++)
                            {
                                LocalValues[col, row, floor] = Values[col, row, floor];
                            }
                        }
                    }
                    // reload the active board
                    String[,] LocalBoard = new String[9, 9];
                    for (int col = 0; col <= 8; col++)
                    {
                        for (int row = 0; row <= 8; row++)
                        {
                            LocalBoard[col, row] = GameText[col, row].Text;
                        }
                    }
                    HaveAGuess(sender, e, LocalValues, LocalBoard);

                    // when we get back from HaveAGuess, unless the game is solved, we have to restore the sent board
                    if (Solved < 81)
                    {
                        for (int col = 0; col <= 8; col++)
                        {
                            for (int row = 0; row <= 8; row++)
                            {
                                if (GameText[col, row].Text != GuessBoard[col, row])
                                    GameText[col, row].Text = GuessBoard[col, row];
                            }
                        }
                    }
                }
                else
                {
                    // since we are not going to try a next level guess, we have to restore the gameboard to prepare for another guess here
                    for (int col = 0; col <= 8; col++)
                    {
                        for (int row = 0; row <= 8; row++)
                        {
                            if (GameText[col, row].Text != GuessBoard[col, row])
                                GameText[col, row].Text = GuessBoard[col, row];
                        }
                    }
                }
            }
            // Reset DeadEnd
            DeadEnd = false;
            // reduce the depth count
            Depth--;
        }

        private void ValidateBoard_Click(object sender, EventArgs e)
        {
            DeadEnd = false;
            Say.Text = "";
            Solved = 0; // to prevent an ifinite loop, we must keep track of the number of solved squares
            //Guesses = 0;
            AloneColRow = 0;
            AloneStack = 0;
            AloneBox = 0;
            Difficulty = 0;
            // Values[] must be reset to make this routine serially reusable
            // setup the Values array
            for (int col = 0; col < 9; col++)
            {
                for (int row = 0; row < 9; row++)
                {
                    for (int floor = 0; floor < 10; floor++)
                        Values[col, row, floor] = floor;
                }
            }

            // as all the entries have already been checked for values from 1-9 IF there is a value,
            // we are now ready to move the text entries to the 0,0,0 level of the array
            // if there is a null entry in the text field, the array already contains a zero (0)
            for (int col = 0; col <= 8; col++)
            {
                for (int row = 0; row <= 8; row++)
                {
                    GameText[col, row].BackColor = System.Drawing.SystemColors.Info;
                    if (GameText[col, row].Text != "")
                    {
                        Values[col, row, 0] = Convert.ToInt16(GameText[col, row].Text);
                        Solved++;
                    }
                }
            }
            SpecialBackgroundColors();
            // there are three levels of checking of the initial user input. If the setup
            // is invalid, the game board cannot produce a valid solution
            // check every column of user input for duplicate values == invalid setup
            for (int x1 = 0; x1 <= 8; x1++)
            {
                if (Say.Text == "logical error in starting values")
                    return;
                if (!validate_col(x1)) return;
            }

            // check every row of user input for duplicate values == invalid setup
            for (int y1 = 0; y1 <= 8; y1++)
            {
                if (Say.Text == "logical error in starting values")
                    return;
                if (!validate_row(y1)) return;
            }

            // check each of the 9 3 x 3 boxes for duplicate values == invalid setup
            for (int y1 = 0; y1 <= 8; y1 = y1 + 3)
            {
                if (Say.Text == "logical error in starting values")
                    return;
                for (int x1 = 0; x1 <= 8; x1 = x1 + 3)
                {
                    if (!validate_box(x1, y1)) return;
                }
            }
        }

        private void Input_Click(object sender, EventArgs e)
        {
            chooseInputFileDialog.ShowDialog();
        }

        private void Output_Click(object sender, EventArgs e)
        {
            chooseOutputFileDialog.ShowDialog();
        }

        private void OnInputFileDialogOK(object sender, CancelEventArgs e)
        {
            LoadGame.Text = chooseInputFileDialog.FileName;
        }

        private void OnOutputFileDialogOK(object sender, CancelEventArgs e)
        {
            SaveGame.Text = chooseOutputFileDialog.FileName;
        }
    }
}