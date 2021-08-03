README for Clipper2

This package provides convenient MATLAB front ends to Angus Johnson's polygon
clipping routines (https://sourceforge.net/projects/polyclipping/).

FUNCTIONS:
   polyclip -- find the difference, intersection, xor or union of two polygons
   polyout  -- outset (or inset) a polygon's vertices
Each polygon is specified by a vector of x values and a vector of y values.
The work is done by the MEX file private/clipper (which must be compiled).


INSTALLATION

Make '.../clipper2/mex code' the working directory, and compile for your platform:

mex '-D__int64=__int64_t' 'clipper.cpp' 'mexclipper.cpp'

Run polyclip_test and polyout_test to check that everything works allright.

CREDITS

This is distributed by Prof. Erik A Johnson <JohnsonE@usc.edu>.
The MEX wrapper is a re-write of one originally written by Emmett
    (http://www.mathworks.com/matlabcentral/fileexchange/36241-polygon-clipping-and-offsetting)
	which was based on Sebastian Holz's Polygon Clipper mex wrapper for the GPC library
    (https://www.mathworks.com/matlabcentral/fileexchange/8818-polygon-clipper)

