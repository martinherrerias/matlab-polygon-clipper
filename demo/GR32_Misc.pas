unit GR32_Misc;

(* BEGIN LICENSE BLOCK *********************************************************
 * Version: MPL 1.1
 *
 * The contents of this file are subject to the Mozilla Public License Version
 * 1.1 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 * http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the
 * License.
 *
 * The Original Code is GR32_Misc.
 * The Initial Developer of the Original Code is Angus Johnson and is
 * Copyright (C) 2009-2010 the Initial Developer. All Rights Reserved.
 *
 * Version 3.9a (Last updated 28-May-10)
 *
 * END LICENSE BLOCK **********************************************************)

interface

{$IFDEF COMPILER7}
{$WARN UNSAFE_CODE OFF}
{$ENDIF}

uses
  Windows, Types,
  Classes, SysUtils, Math, GR32, GR32_LowLevel, GR32_Blend, GR32_Transforms,
  GR32_Math, GR32_Polygons;

type
  TArrayOfArrayOfArrayOfFixedPoint = array of TArrayOfArrayOfFixedPoint;

procedure OffsetPoints(var pts: TArrayOfFixedPoint; dx, dy: single); overload;
function FixedPoints(const ppts: TArrayOfArrayOfFloatPoint): TArrayOfArrayOfFixedPoint;

function CreateMaskFromPolygon(bitmap: TBitmap32;
  const polygons: TArrayOfArrayOfFixedPoint): TBitmap32; overload;
procedure ApplyMaskToAlpha(bitmap, mask: TBitmap32; invertMask: boolean = false);
procedure ApplyMask(modifiedBmp, originalBmp, maskBmp: TBitmap32; invertMask: boolean = false);
procedure SimpleShadow(bitmap: TBitmap32; const pts: TArrayOfArrayOfFixedPoint;
  dx,dy,fadeRate: integer; shadowColor: TColor32;
  closed: boolean = false; NoInteriorBleed: boolean = false); overload;
procedure Simple3D(bitmap: TBitmap32; const pts: TArrayOfArrayOfFixedPoint;
  dx,dy,fadeRate: integer; topLeftColor, bottomRightColor: TColor32); overload;
function GetEllipsePoints(const ellipseRect: TFloatRect): TArrayOfFloatPoint;

const
  MAXIMUM_SHADOW_FADE = 0;
  MEDIUM_SHADOW_FADE  = 5;
  MINIMUM_SHADOW_FADE = 10;
  NO_SHADOW_FADE      = 11; //anything > 10

implementation

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

procedure OffsetPoints(var pts: TArrayOfFixedPoint; dx, dy: single);
var
  i: integer;
  dxFixed, dyFixed: TFixed;
begin
  dxFixed := Fixed(dx);
  dyFixed := Fixed(dy);
  for i := 0 to high(pts) do
    with pts[i] do
    begin
      X := X + dxFixed;
      Y := Y + dyFixed;
    end;
end;
//------------------------------------------------------------------------------

function FixedPoints(const ppts: TArrayOfArrayOfFloatPoint): TArrayOfArrayOfFixedPoint;
var
  i,j: integer;
begin
  setlength(result, length(ppts));
  for i := 0 to high(ppts) do
  begin
    setlength(result[i], length(ppts[i]));
    for j := 0 to high(ppts[i]) do
      result[i][j] := FixedPoint(ppts[i][j]);
  end;
end;
//------------------------------------------------------------------------------

function CreateMaskFromPolygon(bitmap: TBitmap32;
  const polygons: TArrayOfArrayOfFixedPoint): TBitmap32;
var
  i, highI: integer;
begin
  result := TBitmap32.create;
  with bitmap do result.SetSize(width,height);
  highI := high(polygons);
  if highI < 0 then exit;
  {$IFDEF GR32_PolygonsEx}
  with TPolygon32Ex.Create do
  {$ELSE}
  with TPolygon32.Create do
  {$ENDIF}
  try
    Closed := true;
    Antialiased := true;
    AntialiasMode := am16times;
    FillMode := pfAlternate;
    AddPoints(polygons[0][0], length(polygons[0]));
    for i := 1 to highI do
    begin
      NewLine;
      AddPoints(polygons[i][0], length(polygons[i]));
    end;
    Draw(result, clBlack32, clWhite32);
  finally
    free;
  end;
end;
//------------------------------------------------------------------------------

procedure ApplyMaskToAlpha(bitmap, mask: TBitmap32; invertMask: boolean = false);
var
  i: integer;
  bitmapColor, maskColor: PColor32Entry;
begin
  //nb: this only works when bitmap.DrawMode = Blend ...
  if (bitmap.DrawMode = dmOpaque) or
    (bitmap.Width <> mask.Width) or (bitmap.Height <> mask.Height) then exit;

  bitmapColor := @bitmap.Bits[0];
  maskColor := @mask.Bits[0];
  if invertMask then
    for i := 1 to bitmap.Width * bitmap.Height do
    begin
      //ie masked area (white) becomes transparent, background is opaque
      bitmapColor.A := (bitmapColor.A * (255-maskColor.A)) shr 8;
      inc(bitmapColor);
      inc(maskColor);
    end
  else
    for i := 1 to bitmap.Width * bitmap.Height do
    begin
      //ie masked area (white) becomes opaque, background is transparent
      bitmapColor.A := (bitmapColor.A * maskColor.A) shr 8;
      inc(bitmapColor);
      inc(maskColor);
    end;
end;
//------------------------------------------------------------------------------

procedure ApplyMask(modifiedBmp, originalBmp, maskBmp: TBitmap32; invertMask: boolean = false);
var
  i: integer;
  origClr, modClr, mskClr: PColor32Entry;
begin
  if not assigned(originalBmp) or not assigned(maskBmp) or
    (originalBmp.Width <> modifiedBmp.Width) or
    (originalBmp.Height <> modifiedBmp.Height) or
    (originalBmp.Height <> maskBmp.Height) or
    (originalBmp.Height <> maskBmp.Height) then exit;

  origClr := @originalBmp.Bits[0];
  modClr := @modifiedBmp.Bits[0];
  mskClr := @maskBmp.Bits[0];
  for i := 1 to originalBmp.Width * originalBmp.Height do
  begin
    //black pixel in mask -> replace modified color with original color
    //white pixel in mask -> keep modified color
    if invertMask then
      MergeMemEx(origClr.ARGB, modClr.ARGB, 255- mskClr.B) else
      MergeMemEx(origClr.ARGB, modClr.ARGB, mskClr.B);
    inc(origClr);
    inc(modClr);
    inc(mskClr);
  end;
  EMMS;
end;
//------------------------------------------------------------------------------

procedure SimpleLine(bitmap: TBitmap32;
  const pts: array of TFixedPoint; color: TColor32; closed: boolean = false);
var
  i, j: integer;
begin
  j := high(pts);
  if j < 1 then exit;
  for i := 1 to j do
    bitmap.LineXS(pts[i-1].X, pts[i-1].Y,pts[i].X, pts[i].Y, color);
  if closed then bitmap.LineXS(pts[j].X, pts[j].Y,pts[0].X, pts[0].Y, color);
end;
//------------------------------------------------------------------------------

procedure SimpleShadow(bitmap: TBitmap32; const pts: TArrayOfArrayOfFixedPoint;
  dx, dy, fadeRate: integer; shadowColor: TColor32;
  closed: boolean = false; NoInteriorBleed: boolean = false);
var
  i, j, maxD: integer;
  sx,sy, a, alpha, alphaLinear, alphaExp, dRate: single;
  p: TArrayOfFixedPoint;
  originalBitmap, maskBitmap: TBitmap32;
  sc: TColor32;
begin
  if ((dx = 0) and (dy = 0)) or (length(pts) = 0) then exit;

  if abs(dy) > abs(dx) then
  begin
    maxD := abs(dy);
    sy := sign(dy);
    sx := dx/maxD;
  end else
  begin
    maxD := abs(dx);
    sx := sign(dx);
    sy := dy/maxD;
  end;

  if fadeRate <= MAXIMUM_SHADOW_FADE then dRate := 0.05
  else if fadeRate >= MINIMUM_SHADOW_FADE then dRate := 0.95
  else dRate := fadeRate/10;
  alpha := AlphaComponent(shadowColor);
  alphaLinear := alpha*dRate/maxD;
  alphaExp := exp(ln(dRate)/maxD);

  NoInteriorBleed := NoInteriorBleed and closed;
  if NoInteriorBleed then
  begin
    originalBitmap := TBitmap32.Create;
    originalBitmap.Assign(bitmap);
    maskBitmap := CreateMaskFromPolygon(bitmap,pts);
  end else
  begin
    originalBitmap := nil;
    maskBitmap := nil;
  end;

  try
    a := alpha;
    sc := shadowColor;
    for j := 0 to high(pts) do
    begin
      alpha := a;
      shadowColor := sc;
      p := copy(pts[j], 0, length(pts[j]));
      for i := 1 to maxD do
      begin
        SimpleLine(bitmap, p, shadowColor, closed);
        alpha := alpha * alphaExp;
        if fadeRate < NO_SHADOW_FADE then
          shadowColor := SetAlpha(shadowColor, round(alpha - i*alphaLinear));
        OffsetPoints(p, sx, sy);
      end;
    end;
    if assigned(originalBitmap) then
      ApplyMask(bitmap, originalBitmap, maskBitmap);
  finally
    FreeAndNil(originalBitmap);
    FreeAndNil(maskBitmap);
  end;
end;
//------------------------------------------------------------------------------

procedure Simple3D(bitmap: TBitmap32; const pts: TArrayOfArrayOfFixedPoint;
  dx,dy,fadeRate: integer; topLeftColor, bottomRightColor: TColor32); overload;
var
  mask, orig: TBitmap32;
begin
  orig := TBitmap32.Create;
  mask := CreateMaskFromPolygon(bitmap,pts);
  try
    orig.Assign(bitmap);
    SimpleShadow(bitmap, pts, -dx, -dy, fadeRate, bottomRightColor, true);
    SimpleShadow(bitmap, pts, dx, dy, fadeRate, topLeftColor, true);
    ApplyMask(bitmap, orig, mask, true);
  finally
    orig.Free;
    mask.Free;
  end;
end;
//------------------------------------------------------------------------------

function GetCBezierPoints(const control_points: array of TFloatPoint): TArrayOfFloatPoint;
var
  i, j, arrayLen, resultCnt: integer;
  ctrlPts: array [ 0..3] of TFloatPoint;
const
  cbezier_tolerance = 0.5;
  half = 0.5;

  procedure RecursiveCBezier(const p1, p2, p3, p4: TFloatPoint);
  var
    p12, p23, p34, p123, p234, p1234: TFloatPoint;
  begin
    //assess flatness of curve ...
    //http://groups.google.com/group/comp.graphics.algorithms/tree/browse_frm/thread/d85ca902fdbd746e
    if abs(p1.x + p3.x - 2*p2.x) + abs(p2.x + p4.x - 2*p3.x) +
      abs(p1.y + p3.y - 2*p2.y) + abs(p2.y + p4.y - 2*p3.y) < cbezier_tolerance then
    begin
      if resultCnt = length(result) then
        setLength(result, length(result) +128);
      result[resultCnt] := p4;
      inc(resultCnt);
    end else
    begin
      p12.X := (p1.X + p2.X) *half;
      p12.Y := (p1.Y + p2.Y) *half;
      p23.X := (p2.X + p3.X) *half;
      p23.Y := (p2.Y + p3.Y) *half;
      p34.X := (p3.X + p4.X) *half;
      p34.Y := (p3.Y + p4.Y) *half;
      p123.X := (p12.X + p23.X) *half;
      p123.Y := (p12.Y + p23.Y) *half;
      p234.X := (p23.X + p34.X) *half;
      p234.Y := (p23.Y + p34.Y) *half;
      p1234.X := (p123.X + p234.X) *half;
      p1234.Y := (p123.Y + p234.Y) *half;
      RecursiveCBezier(p1, p12, p123, p1234);
      RecursiveCBezier(p1234, p234, p34, p4);
    end;
  end;

begin
  //first check that the 'control_points' count is valid ...
  arrayLen := length(control_points);
  if (arrayLen < 4) or ((arrayLen -1) mod 3 <> 0) then exit;

  setLength(result, 128);
  result[0] := control_points[0];
  resultCnt := 1;
  for i := 0 to (arrayLen div 3)-1 do
  begin
    for j := 0 to 3 do
      ctrlPts[j] := control_points[i*3 +j];
    RecursiveCBezier(ctrlPts[0], ctrlPts[1], ctrlPts[2], ctrlPts[3]);
  end;
  SetLength(result,resultCnt);
end;
//------------------------------------------------------------------------------

function GetEllipsePoints(const ellipseRect: TFloatRect): TArrayOfFloatPoint;
const
  //Magic constant = 2/3*(1-cos(90deg/2))/sin(90deg/2) = 2/3*(sqrt(2)-1) = 0.27614
  offset: single = 0.276142375;
var
  midx, midy, offx, offy: single;
  pts: array [0..12] of TFloatPoint;
begin
  with ellipseRect do
  begin
    if (abs(Left - Right) <= 0.5) and (abs(Top - Bottom) <= 0.5) then
    begin
      setlength(result,1);
      result[0] := FloatPoint(Left,Top);
      exit;
    end;

    midx := (right + left)/2;
    midy := (bottom + top)/2;
    offx := (right - left) * offset;
    offy := (bottom - top) * offset;
    //draws an ellipse starting at angle 0 and moving anti-clockwise ...
    pts[0]  := FloatPoint(right, midy);
    pts[1]  := FloatPoint(right, midy - offy);
    pts[2]  := FloatPoint(midx + offx, top);
    pts[3]  := FloatPoint(midx, top);
    pts[4]  := FloatPoint(midx - offx, top);
    pts[5]  := FloatPoint(left, midy - offy);
    pts[6]  := FloatPoint(left, midy);
    pts[7]  := FloatPoint(left, midy + offy);
    pts[8]  := FloatPoint(midx - offx, bottom);
    pts[9]  := FloatPoint(midx, bottom);
    pts[10] := FloatPoint(midx + offx, bottom);
    pts[11] := FloatPoint(right, midy + offy);
    pts[12] := pts[0];
  end;
  result := GetCBezierPoints(pts);
end;
//------------------------------------------------------------------------------

end.
