function r = polyout(p,delta,jointype,par,scale)
% q = POLYOUT(p,delta)  Outset (expand/inflate) or contract the polygon p by a distance delta.
% q = POLYOUT(p,delta,jointype,par) use vertex-offseting-type jointype, with parameter par
% r = POLYOUT(p,delta,jointype,par,scale) specify a custom scale for int64 conversion.
%
%   p: polygon object(s) or structure(s) with fields (x,y,hole). Anything else will be passed 
%       to polygon(), so it should work with [xx,yy], [w,h], etc.
%   delta: distance to offset, positive for outset (expand/inflate), negative to contract.
%   jointype: vertex-offseting, or 'expanded-corner' type
%       http://www.angusj.com/delphi/clipper/documentation/Docs/Units/ClipperLib/Types/JoinType.htm
%       'm' or 'miter' (default) - exact corners but square at small angles
%               Optional parameter par is the miter-limit, a multiple of delta; if the corner
%               point would be moved more than par·delta, then it is squared-off instead. 
%               The default value, as well as the minimum allowed, is 2.
%       's' or 'square' - square off corners, par is ignored.
%       'r' or 'round'  - round corners. Required par sets the precision of points along the arc
%               (smaller par - more points along the arc); for a 180° arc, the number of points
%               is (pi/acos(1-par/delta) using the same scaling as the polygon points.
%   par: scalar parameter for jointype 'm' or 'r' (see above).
%   scale: the clipper library internally uses 62-bit integers, so floating-point numbers must be
%       scaled-up, offseted, and scaled-down for reasonable precision. The default scale factor is
%       2^32, but can be modified when the scale of the polygons p,q is far from O(1).
%   q: resulting polygon object, or an array of polygons, in case of fragmented output, i.e. holes, 
%       split polygons, etc.
%
% See also CLIPPER, POLYCLIP, POLYGON
%
% Modified from: (c)2015-17, Prof. Erik A. Johnson <JohnsonE@usc.edu>, 01/28/17

    narginchk(2,5)

    if nargin < 3 || isempty(jointype), jointype = 'square'; end
    if ~ischar(jointype), jointype = 'x'; end % force crash below (*)
    jointype = lower(jointype(1));
   
    if nargin < 4, par = []; 
    elseif jointype ~= 's'
        assert(isscalar(par),'polyout:par','jointype parameter must be scalar');
    end
  
    if nargin < 5, scale = NaN; end

    switch jointype
        case 'r'
            if isempty(par), par = (abs(delta)+(delta==0))*(1-cosd(5)); end % about every 5 degrees
            parArg = {scale*par}; % scale
        case 'm'
            if isempty(par), par = 2; end
            parArg = {par}; % relative to delta, don't scale
        case 's'
            parArg = {};
        otherwise
            error('polyout:jointype',...
                'jointype must be a string/character and start with ''m'', ''s'', or ''r''');
    end


    pwaspacked = isstruct(p);

    if isnan(scale)
        if pwaspacked && ~isempty(p), scale = mode([p.scale]);
        else, scale = polygon.SCALE; 
        end
    end
    scale = double(scale);
    
    if ~pwaspacked
        hiRange = int64(2)^62-1-int64(max(0,delta)*scale); % leave space for outset
        p = pack(p,scale,hiRange); 
        delta = delta*scale;
    end

    r = clipper(p,delta,jointype,parArg{:});
    [r.scale] = deal(scale);
 
    if isempty(r), r = polygon.empty; 
    elseif ~pwaspacked
        r = polygon.unpack(r);
        % r = fixorientation(r,true);
    end
end
