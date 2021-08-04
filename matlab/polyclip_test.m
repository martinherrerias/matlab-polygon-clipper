function polyclip_test(FT)
% function to test polyclip.m

if nargin < 1, FT = []; end

%clear all; close all;
p = polygon(1,1);
q0 = [polygon(1,1),polygon(0.7,0.7,true)];

methodstrs = {'dif' 'difference'
              'int' 'intersection'
              'xor' 'Xor'
              'uni' 'union'
             };

offset = [0.3,0.5;0.3,1.2];
methods = 0:3;
clf();

if ~isempty(FT)
    N = 50;
    randx = @(x,N) rand(N,1)*(max(x)-min(x)) + min(x);
end

for ii = 1:size(offset,1)
    
    q = polytranslate(q0,offset(ii,:));
    if ~isempty(FT)
        X = randx([[p.x],[q.x]],N);
        Y = randx([[p.y],[q.y]],N);
    end

	for jj = 1:numel(methods)
        
		r = polyclip(p,q,methods(jj));
		subplot(length(offset),length(methods),jj+(ii-1)*length(methods));
        polyplot(r,'g','none');
        polyplot(p,'none','b','LineWidth',2);
		polyplot(q,'none','r','LineWidth',2);
        if ~isempty(FT)
            hold on;
            f = insidepolygon(r,X,Y,FT);
            plot(X(f),Y(f),'b+');
            plot(X(~f),Y(~f),'mx');
        end
		title(sprintf('%d ''%s'' (%s)',methods(jj),methodstrs{methods(jj)+1,1},methodstrs{methods(jj)+1,2}));
        axis equal
		axis(reshape((eye(2)+[1 -1;-1 1]/20)*reshape(axis,2,[]),1,[])); % expand axis limits a bit
	end
end

end