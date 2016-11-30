# Mudpie
A C#-based MUD/MUCK/MOO/MU*

Mudpie is a C# implementation of a MU*-style text-based game using the Telnet protocol.

The world supports programming via the new Roslyn C# Scripting API, which means, you can write regular C# instead of Forth, MUF, or MPI.

This is a proof-of-concept that uses Redis as its backing data store.  You must install Redis to run this proof of concept.

Basic commands like @dig, @name, and @desc are implemented, but useful things like @open and @link are left as an exercise for the reader at the moment.
