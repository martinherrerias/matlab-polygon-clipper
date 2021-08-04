function r = polyclip(p,q,method,pFT,qFT,scale,nbits)
% r = POLYCLIP(p,q,method) Clip two polygons, according to method (intersect, union, xor, diff).
% r = POLYCLIP(P,q,method,pFT,qFT) specify Fill-Type for each polygon set (default is Even-Odd).
% r = POLYCLIP(P,q,method,pFT,qFT,scale) specify a custom scale for int64 conversion.
%
%   p, q: polygon object(s) or structure(s) with fields (x,y,hole). Anything else will be passed 
%       to polygon(), so it should work with [xx,yy], [w,h], etc.
%   CAUTION: when p and/or q are arrays of polygons, their vertex orientation can affect the way
%       holes are interpreted (see fill-types below). In most cases, the polygon constructor makes 
%       sure that polygons with p.hole = true are oriented opposite to 'outer' polygons (~p.hole)
%       so that using fill-types 0-2 results in predictable results. Call x = fixorientation(x) 
%       beforehand when tampering with vertices and/or hole flags of polygon x.
%   method: operation to perform:
%       0 or 'dif' - difference (P1-P2) [default]
%       1 or 'int' - intersection
%       2 or 'xor' - Xor
%       3 or 'uni' - Union
%   pFt, qFT: Fill-type of provided polygons p and q, see the following link for an explanation:
%   http://www.angusj.com/delphi/clipper/documentation/Docs/Units/ClipperLib/Types/PolyFillType.htm
%       0 or 'eo' - Even-Odd (default)
%       1 or 'nz' - Non-Zero
%       2 or 'pos' - Positive
%       3 or 'neg' - Negative
%   scale: the clipper library internally uses 62-bit integers, so floating-point numbers must be
%       scaled-up, clipped, and scaled-down for reasonable precision. The default scale factor is
%       polygon.SCALE (2^32), but can be modified when polygon sizes are far from O(1).
%   r: resulting polygon object, or an array of polygons, in case of fragmented output, i.e. holes, 
%       split polygons, etc.
%
% See also CLIPPER, POLYOUT, POLYGON.
% 
% Modified from: (c) 2015-17, Prof. Erik A. Johnson <JohnsonE@usc.edu>, 01/29/17

    % Rewrite polygon.isvoid (must work with pre-packed structures)
    isvoid = @(P) isempty(P) || all(arrayfun(@(j) numel(P(j).x) < 3,1:numel(P)));

    narginchk(2,6);
    assert((isa(p,'polygon') || isstruct(p)) && (isa(q,'polygon') || isstruct(q)),...
        'Two polygons/structures are required');
    
    if nargin < 3, method = 0; end
    if ischar(method), method = find(lower(method(1))=='dixu') - 1; end
    if ~isscalar(method) || ~any(method==0:3)
        error('polyclip:method',...
            'method must be an integer (0 to 3) or {''diff'',''int'',''xor'',''union''}.');
    end
    
    if nargin < 4 || isempty(pFT), pFT = 0; end
    if nargin < 5 || isempty(qFT), qFT = pFT; end
    if ischar(pFT), pFT = find(strcmpi(pFT,{'eo','nz','pos','neg'})) - 1; end
    if ischar(qFT), qFT = find(strcmpi(qFT,{'eo','nz','pos','neg'})) - 1 ; end
    assert(isscalar(pFT) && any(pFT == 0:3) && isscalar(qFT) && any(qFT == 0:3),'polyclip:FT',...
        'Fill-type(s) must be an integers (0 to 3) or {''eo'',''nz'',''pos'',''neg''}.');
    
    if nargin < 6, scale = NaN; end
    if nargin < 7, nbits = NaN; end
        
    % NOTE: the criterion for "empty" is that area < eps(1), to keep clipper from crashing
    % hopefully in future versions it will just return an empty structure & this can be skipped
    qwaspacked = isstruct(q);
    pwaspacked = isstruct(p);
    
    if isnan(scale)
        if pwaspacked && ~isempty(p), scale = mode([p.scale]); 
        elseif qwaspacked && ~isempty(q), scale = mode([q.scale]);
        else
           scale = polygon.SCALE; 
        end
    end
    
    if isnan(nbits) && (qwaspacked && pwaspacked)
    % If result will be packed, try to return the same integer type as the input(s)
        if pwaspacked && ~isempty(p)
            if isa(p(1).x,'int16'), nbits = 16; else, nbits = 64; end
        end
        if nbits ~= 64 && qwaspacked && ~isempty(q)
            if isa(q(1).x,'int16'), nbits = 16; else, nbits = 64; end 
        end
    end
    
    if qwaspacked
        qisempty = abs(polygon.packedarea(q)) < eps(1);
        if qisempty
            q = struct('x',int64(0),'y',int64(0));
        else
            assert(all([q.scale] == scale),'Non-matching scale(s) for Q'); 
        end
    else
        qisempty = isvoid(q) || abs(area(q)) < eps(1);
        if qisempty
            q = struct('x',int64(0),'y',int64(0));
        else
            q = pack(q,scale);
        end
    end
    
    if pwaspacked
        pisempty = abs(polygon.packedarea(p)) < eps(1);
        if pisempty % drink more water... badum tss
            p = struct('x',int64(0),'y',int64(0));
        else
            assert(all([p.scale] == scale),'Non-matching scale(s) for P'); 
        end
    else
        pisempty = isvoid(p) || abs(area(p)) < eps(1);
        if pisempty
            p = struct('x',int64(0),'y',int64(0));
        else
            p = pack(p,scale);
        end
    end

    if pisempty && qisempty
        if qwaspacked && pwaspacked, r = struct('x',int64(0),'y',int64(0),'scale',scale);
        else, r = polygon.empty;
        end
        return;
    end
        
    % Sort out trivial cases - ACHTUNG! - not so trivial considering pFT, qFT
    % if isempty(q)
    %     switch method
    %         case {0,3}, r = p; return;              % P +/- 0 = P
    %         case {1,2}, r = polygon.empty; return;  % P &/xor 0 = 0
    %     end
    % elseif isempty(p)
    %     switch method
    %         case 0, r = q; return;                    % 0 + Q = Q
    %         case {1,2,3}, r = polygon.empty; return;  % 0 &/xor/- Q = 0
    %     end
    % end

    %if ~isa(p,'polygon'), p = polygon(p); end
    %if ~isa(q,'polygon'), q = polygon(q); end

    r = clipper(p,q,method,pFT,qFT);
    [r.scale] = deal(scale);
    
    if ~(qwaspacked && pwaspacked)
        r = polygon.unpack(r);
    elseif nbits == 16
        for j = 1:numel(r)
           r(j).x = int16(r(j).x);
           r(j).y = int16(r(j).y);
        end
    end
end
        
