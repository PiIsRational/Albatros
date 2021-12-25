## Overview

Albatros is an open source UCI-compatible chess engine. It is not a complete chess programm and would require a UCI-compatible graphical user interface (GUI) (ex : Scid, Arena or Shredder) to be used comfortably.

The Albatros chess engine currently features 3 different evaluation functions for chess. The classical, which work with simple piece square tables (PSQT). And the two NNUE evaluation funtions, which are using the HalfKav2 and HalfKp architectures.Thes classical evaluation function is currently the only evaluation function that is implented efficiently.

## Files

This Distribution of Albatros contains the following files:

* [Readme.md](https://github.com/PiIsRational/Albatros/blob/master/README.md), the File you are currently reading.
* [Albatros Engine](https://github.com/PiIsRational/Albatros/tree/master/Albatros%20Engine), a subdirectory that contains the whole sourcecode.
* [Albatros Engine.sln](https://github.com/PiIsRational/Albatros/blob/master/Albatros%20Engine.sln), the solution file of the programm.
* [License.txt](https://github.com/PiIsRational/Albatros/blob/master/LICENSE), a text file that contains the GNU general public license version 2.
* There sould be a file with a .nnue extention, but there are currently no such files with a requiered strength.

## The UCI protocol and available options

The Universal Chess Interface (UCI) is a standard protocol used to communicate with a chess engine, and is the recommended way to do so for typical graphical user interfaces (GUI) or chess tools. Albatros does currently not support the following options, as they are describerd in the [UCI protocoll](https://www.shredderchess.com/download/div/uci.zip):

 * go
   * searchmoves
   * ponder
   * wtime / btime
   * winc / binc
   * mate
   * movetime
   * increment
   * movestogo
   * infinite
 * ponderhit
 * quit

### This is the list of all the available UCI options in Albatros:

 * #### Threads
   The number of CPU threads used for searching a position and for training. For best performance, set this equal to the number of CPU cores available.
   
 * #### Ponder 
   Let Albatros ponder its next move while the opponent is thinking (currently does not do anything).
