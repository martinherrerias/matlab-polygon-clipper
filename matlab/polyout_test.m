function polyout_test()
% function to test polyout.m

    %clear all; close all;
%     p{1} = polygon([0 1 1 0],[0 0 1 1]);
%     p{2} = polygon([0 1 .75 .5 0 0],[0 0 1 .3 .75 0]);

    props = {'LineWidth' 2};
%     jointypes = {'round' []
%                  'square' []
%                  'miter' []
%                  'miter' 2.5
%                  'miter' 5
%                  };
    deltalist = 0.25:-0.0625:-0.25;
    colors = hsv(length(deltalist));
    colors_light = 1-(1-colors)*.25;
    colors_dark  = colors*.75;
%     ax = [];
%     H = cell(length(p),size(jointypes,1));
%     joinstrs = {1,size(jointypes,1)};
%     for jj = 1:size(jointypes,1)
%         [join,miterfactor]=deal(jointypes{jj,:});
%         if isempty(miterfactor)
%             if strcmp(join,'miter')
%                 infostr = ' (default=2)';
%             else
%                 infostr = '';
%             end
%             miterarg = {};
%         else
%             infostr = sprintf(' (%s)',num2str(miterfactor));
%             miterarg = {miterfactor};
%         end
%         for ii = 1:length(p)
%             for kdelta = 1:length(deltalist)
%                 delta=deltalist(kdelta);
%                 r = polyout(p{ii},delta,join,miterarg{:});
%                 h = polyplot(r,colors_light(kdelta,:),colors_dark(kdelta,:));
%                 if delta==0, set(h,'EdgeColor','k',props{:}); end;
%                 H{ii,jj} = [H{ii,jj}; h];
%             end
%             % p = patch(X{ii},Y{ii},'k','FaceColor','none','EdgeColor','k',props{:});
%         end
%         joinstrs{1,jj} = sprintf('%s%s',join,infostr);
%     end
% 
%     % spread them out
%     UOf = {'UniformOutput' false};
%     widths = cellfun(@(x) max(x)-min(x), cellfun(@(x) cat(1,x{:}),cellfun(@(p) get(p,{'XData'}), H, UOf{:}), UOf{:}));
%     dx = max(widths(:));
%     heights = cellfun(@(y) max(y)-min(y), cellfun(@(y) cat(1,y{:}),cellfun(@(p) get(p,{'YData'}), H, UOf{:}), UOf{:}));
%     dy = max(heights,[],2);
%     gap = 0.25; u = 0.5;
%     dx=round((dx+gap)/u)*u; dy=round((dy+gap)/u)*u;
%     dx=repmat(dx,1,size(H,2)/length(dx)); dx=cumsum([0 dx]); DX=dx(end); dx(end)=[];
%     dy=repmat(dy,size(H,1)/length(dy),1); dy=cumsum([0;dy]); DY=dy(end); dy(end)=[];
%     [dx,dy] = meshgrid(dx,dy);
%     cellfun(@(p,dx,dy) set(p,{'XData'},cellfun(@(x)x+dx,get(p,{'XData'}),UOf{:}),{'YData'},cellfun(@(y)y+dy,get(p,{'YData'}),UOf{:})), H, num2cell(dx), num2cell(dy), UOf{:})
% 
%     % add strings
%     meanx = (max([p{1}.x,p{2}.x])+min([p{1}.x,p{2}.x]))/2; 
%     text(dx(1,:)+meanx,repmat(DY,1,size(dx,2)),joinstrs,'HorizontalAlignment','center')
% 
%     % set axis limits
%     PH = findobj(gcf,'Type','patch');
%         x=get(PH,{'XData'}); x=cat(1,x{:});
%         y=get(PH,{'YData'}); y=cat(1,y{:});
%     axis([[min(x) max(x)]*(eye(2)+[1 -1;-1 1]/100) [min(y) max(y)]*(eye(2)+[1 -1;-1 1]/100)]);
%     axis equal
    %%
    q = [polygon(1,1),polygon(0.5,0.5,true)];
    polyout(q,0.1);

    figure();
    d = 0.1:-0.05:-0.1;
    for j = 1:numel(d)
       h = polyplot(polyout(q,d(j)),colors_light(j,:),colors_dark(j,:));
       if d(j)==0, set(h(1),'EdgeColor','k',props{:}); end
    end
end

