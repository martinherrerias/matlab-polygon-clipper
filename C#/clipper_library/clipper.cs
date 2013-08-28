﻿/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.0.0                                                           *
* Date      :  27 August 2013                                                  *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2013                                         *
*                                                                              *
* License:                                                                     *
* Use, modification & distribution is subject to Boost Software License Ver 1. *
* http://www.boost.org/LICENSE_1_0.txt                                         *
*                                                                              *
* Attributions:                                                                *
* The code in this library is an extension of Bala Vatti's clipping algorithm: *
* "A generic solution to polygon clipping"                                     *
* Communications of the ACM, Vol 35, Issue 7 (July 1992) pp 56-63.             *
* http://portal.acm.org/citation.cfm?id=129906                                 *
*                                                                              *
* Computer graphics and geometric modeling: implementation and algorithms      *
* By Max K. Agoston                                                            *
* Springer; 1 edition (January 4, 2005)                                        *
* http://books.google.com/books?q=vatti+clipping+agoston                       *
*                                                                              *
* See also:                                                                    *
* "Polygon Offsetting by Computing Winding Numbers"                            *
* Paper no. DETC2005-85513 pp. 565-575                                         *
* ASME 2005 International Design Engineering Technical Conferences             *
* and Computers and Information in Engineering Conference (IDETC/CIE2005)      *
* September 24-28, 2005 , Long Beach, California, USA                          *
* http://www.me.berkeley.edu/~mcmains/pubs/DAC05OffsetPolygon.pdf              *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
*                                                                              *
* This is a translation of the Delphi Clipper library and the naming style     *
* used has retained a Delphi flavour.                                          *
*                                                                              *
*******************************************************************************/

//use_int32: When enabled 32bit ints are used instead of 64bit ints. This
//improve performance but coordinate values are limited to the range +/- 46340
//#define use_int32

//use_xyz: adds a Z member to IntPoint. Adds a minor cost to perfomance.
//#define use_xyz

//UseLines: Enables line clipping. Adds a very minor cost to performance.
//#define use_lines

//When enabled, code developed with earlier versions of Clipper 
//(ie prior to ver 6) should compile without changes. 
//In a future update, this compatability code will be removed.
#define use_deprecated


using System;
using System.Collections.Generic;
//using System.Text; //for Int128.AsString() & StringBuilder
//using System.IO; //streamReader & StreamWriter

namespace ClipperLib
{

#if use_int32
  using cInt = Int32;
#else
  using cInt = Int64;
#endif

  using Path = List<IntPoint>;
  using Paths = List<List<IntPoint>>;

#if use_deprecated
  using Polygon = List<IntPoint>;
  using Polygons = List<List<IntPoint>>;
#endif

  public class DoublePoint
  {
    public double X { get; set; }
    public double Y { get; set; }
    public DoublePoint(double x = 0, double y = 0)
    {
      this.X = x; this.Y = y;
    }
    public DoublePoint(DoublePoint dp)
    {
      this.X = dp.X; this.Y = dp.Y;
    }
    public DoublePoint(IntPoint ip)
    {
      this.X = ip.X; this.Y = ip.Y;
    }
  };
  //------------------------------------------------------------------------------

  //ClipperConvert: converts IntPoint to and from DoublePoint based on "scaling_factor"
  public class ClipperConvert
  {

    public ClipperConvert(double scaling_factor) 
    {
      if (ClipperBase.near_zero(scaling_factor))
        throw new ClipperException("Invalid scaling factor");
      scale = scaling_factor; 
    }

    public IntPoint Convert(DoublePoint dp)
    {
      return new IntPoint(Clipper.Round(scale * dp.X), Clipper.Round(scale * dp.Y));
    }

    public DoublePoint Convert(IntPoint ip)
    {
      return new DoublePoint((double)ip.X / scale, (double)ip.Y / scale);
    }

    public Path Convert(List<DoublePoint> dps)
    { 
      Path path = new Path(dps.Count);
      foreach (DoublePoint dp in dps)
        path.Add(new IntPoint(Clipper.Round(scale * dp.X), Clipper.Round(scale * dp.Y)));
      return path;
    }
    
    public List<DoublePoint> Convert(Path path)
    {
      List<DoublePoint> dps = new List<DoublePoint>(path.Count);
      foreach (IntPoint ip in path)
        dps.Add(new DoublePoint((double)ip.X/scale, (double)ip.Y/scale));
      return dps;
    }
    
    private double scale;
  };



  //------------------------------------------------------------------------------
  // PolyTree & PolyNode classes
  //------------------------------------------------------------------------------

  public class PolyTree : PolyNode
  {
      internal List<PolyNode> m_AllPolys = new List<PolyNode>();

      ~PolyTree()
      {
          Clear();
      }
        
      public void Clear() 
      {
          for (int i = 0; i < m_AllPolys.Count; i++)
              m_AllPolys[i] = null;
          m_AllPolys.Clear(); 
          m_Childs.Clear(); 
      }
        
      public PolyNode GetFirst()
      {
          if (m_Childs.Count > 0)
              return m_Childs[0];
          else
              return null;
      }

      public int Total
      {
          get { return m_AllPolys.Count; }
      }

  }
        
  public class PolyNode 
  {
      internal PolyNode m_Parent;
      internal Path m_polygon = new Path();
      internal int m_Index;
      internal List<PolyNode> m_Childs = new List<PolyNode>();

      private bool IsHoleNode()
      {
          bool result = true;
          PolyNode node = m_Parent;
          while (node != null)
          {
              result = !result;
              node = node.m_Parent;
          }
          return result;
      }

      public int ChildCount
      {
          get { return m_Childs.Count; }
      }

      public Path Contour
      {
          get { return m_polygon; }
      }

      internal void AddChild(PolyNode Child)
      {
          int cnt = m_Childs.Count;
          m_Childs.Add(Child);
          Child.m_Parent = this;
          Child.m_Index = cnt;
      }

      public PolyNode GetNext()
      {
          if (m_Childs.Count > 0) 
              return m_Childs[0]; 
          else
              return GetNextSiblingUp();        
      }
  
      internal PolyNode GetNextSiblingUp()
      {
          if (m_Parent == null)
              return null;
          else if (m_Index == m_Parent.m_Childs.Count - 1)
              return m_Parent.GetNextSiblingUp();
          else
              return m_Parent.m_Childs[m_Index + 1];
      }

      public List<PolyNode> Childs
      {
          get { return m_Childs; }
      }

      public PolyNode Parent
      {
          get { return m_Parent; }
      }

      public bool IsHole
      {
          get { return IsHoleNode(); }
      }

      public bool IsOpen { get; set; }
  }


  //------------------------------------------------------------------------------
  // Int128 struct (enables safe math on signed 64bit integers)
  // eg Int128 val1((Int64)9223372036854775807); //ie 2^63 -1
  //    Int128 val2((Int64)9223372036854775807);
  //    Int128 val3 = val1 * val2;
  //    val3.ToString => "85070591730234615847396907784232501249" (8.5e+37)
  //------------------------------------------------------------------------------

  internal struct Int128
  {
    private Int64 hi;
    private UInt64 lo;

    public Int128(Int64 _lo)
    {
      lo = (UInt64)_lo;
      if (_lo < 0) hi = -1;
      else hi = 0;
    }

    public Int128(Int64 _hi, UInt64 _lo)
    {
      lo = _lo;
      hi = _hi;
    }

    public Int128(Int128 val)
    {
      hi = val.hi;
      lo = val.lo;
    }

    public bool IsNegative()
    {
      return hi < 0;
    }

    public static bool operator ==(Int128 val1, Int128 val2)
    {
      if ((object)val1 == (object)val2) return true;
      else if ((object)val1 == null || (object)val2 == null) return false;
      return (val1.hi == val2.hi && val1.lo == val2.lo);
    }

    public static bool operator !=(Int128 val1, Int128 val2)
    {
      return !(val1 == val2);
    }

    public override bool Equals(System.Object obj)
    {
      if (obj == null || !(obj is Int128))
        return false;
      Int128 i128 = (Int128)obj;
      return (i128.hi == hi && i128.lo == lo);
    }

    public override int GetHashCode()
    {
      return hi.GetHashCode() ^ lo.GetHashCode();
    }

    public static bool operator >(Int128 val1, Int128 val2)
    {
      if (val1.hi != val2.hi)
        return val1.hi > val2.hi;
      else
        return val1.lo > val2.lo;
    }

    public static bool operator <(Int128 val1, Int128 val2)
    {
      if (val1.hi != val2.hi)
        return val1.hi < val2.hi;
      else
        return val1.lo < val2.lo;
    }

    public static Int128 operator +(Int128 lhs, Int128 rhs)
    {
      lhs.hi += rhs.hi;
      lhs.lo += rhs.lo;
      if (lhs.lo < rhs.lo) lhs.hi++;
      return lhs;
    }

    public static Int128 operator -(Int128 lhs, Int128 rhs)
    {
      return lhs + -rhs;
    }

    public static Int128 operator -(Int128 val)
    {
      if (val.lo == 0)
        return new Int128(-val.hi, 0);
      else
        return new Int128(~val.hi, ~val.lo + 1);
    }

    //nb: Constructing two new Int128 objects every time we want to multiply longs  
    //is slow. So, although calling the Int128Mul method doesn't look as clean, the 
    //code runs significantly faster than if we'd used the * operator.

    public static Int128 Int128Mul(Int64 lhs, Int64 rhs)
    {
      bool negate = (lhs < 0) != (rhs < 0);
      if (lhs < 0) lhs = -lhs;
      if (rhs < 0) rhs = -rhs;
      UInt64 int1Hi = (UInt64)lhs >> 32;
      UInt64 int1Lo = (UInt64)lhs & 0xFFFFFFFF;
      UInt64 int2Hi = (UInt64)rhs >> 32;
      UInt64 int2Lo = (UInt64)rhs & 0xFFFFFFFF;

      //nb: see comments in clipper.pas
      UInt64 a = int1Hi * int2Hi;
      UInt64 b = int1Lo * int2Lo;
      UInt64 c = int1Hi * int2Lo + int1Lo * int2Hi;

      UInt64 lo;
      Int64 hi;
      hi = (Int64)(a + (c >> 32));

      unchecked { lo = (c << 32) + b; }
      if (lo < b) hi++;
      Int128 result = new Int128(hi, lo);
      return negate ? -result : result;
    }

    public static Int128 operator /(Int128 lhs, Int128 rhs)
    {
      if (rhs.lo == 0 && rhs.hi == 0)
        throw new ClipperException("Int128: divide by zero");

      bool negate = (rhs.hi < 0) != (lhs.hi < 0);
      if (lhs.hi < 0) lhs = -lhs;
      if (rhs.hi < 0) rhs = -rhs;

      if (rhs < lhs)
      {
        Int128 result = new Int128(0);
        Int128 cntr = new Int128(1);
        while (rhs.hi >= 0 && !(rhs > lhs))
        {
          rhs.hi <<= 1;
          if ((Int64)rhs.lo < 0) rhs.hi++;
          rhs.lo <<= 1;

          cntr.hi <<= 1;
          if ((Int64)cntr.lo < 0) cntr.hi++;
          cntr.lo <<= 1;
        }
        rhs.lo >>= 1;
        if ((rhs.hi & 1) == 1)
          rhs.lo |= 0x8000000000000000;
        rhs.hi = (Int64)((UInt64)rhs.hi >> 1);

        cntr.lo >>= 1;
        if ((cntr.hi & 1) == 1)
          cntr.lo |= 0x8000000000000000;
        cntr.hi >>= 1;

        while (cntr.hi != 0 || cntr.lo != 0)
        {
          if (!(lhs < rhs))
          {
            lhs -= rhs;
            result.hi |= cntr.hi;
            result.lo |= cntr.lo;
          }
          rhs.lo >>= 1;
          if ((rhs.hi & 1) == 1)
            rhs.lo |= 0x8000000000000000;
          rhs.hi >>= 1;

          cntr.lo >>= 1;
          if ((cntr.hi & 1) == 1)
            cntr.lo |= 0x8000000000000000;
          cntr.hi >>= 1;
        }
        return negate ? -result : result;
      }
      else if (rhs == lhs)
        return new Int128(1);
      else
        return new Int128(0);
    }

    public double ToDouble()
    {
      const double shift64 = 18446744073709551616.0; //2^64
      if (hi < 0)
      {
        if (lo == 0)
          return (double)hi * shift64;
        else
          return -(double)(~lo + ~hi * shift64);
      }
      else
        return (double)(lo + hi * shift64);
    }

  };

  //------------------------------------------------------------------------------
  //------------------------------------------------------------------------------

  public struct IntPoint
  {
    public cInt X;
    public cInt Y;
#if use_xyz
    public cInt Z;
    public IntPoint(cInt x = 0, cInt y = 0, cInt z = 0)
    {
      this.X = x; this.Y = y; this.Z = z;
    }
    public IntPoint(IntPoint pt)
    {
      this.X = pt.X; this.Y = pt.Y; this.Z = pt.Z;
    }
#else
    public IntPoint(cInt X, cInt Y)
    {
        this.X = X; this.Y = Y;
    }
    public IntPoint(IntPoint pt)
    {
        this.X = pt.X; this.Y = pt.Y;
    }
#endif

    public static bool operator ==(IntPoint a, IntPoint b)
    {
      return a.X == b.X && a.Y == b.Y;
    }

    public static bool operator !=(IntPoint a, IntPoint b)
    {
      return a.X != b.X  || a.Y != b.Y; 
    }

    public override bool Equals(object obj)
    {
      if (obj == null) return false;
      if (obj is IntPoint)
      {
        IntPoint a = (IntPoint)obj;
        return (X == a.X) && (Y == a.Y);
      }
      else return false;
    }

    public override int GetHashCode()
    {
      //simply prevents a compiler warning
      return base.GetHashCode();
    }
}


  public struct IntRect
  {
    public cInt left;
    public cInt top;
    public cInt right;
    public cInt bottom;

    public IntRect(cInt l, cInt t, cInt r, cInt b)
    {
      this.left = l; this.top = t;
      this.right = r; this.bottom = b;
    }
    public IntRect(IntRect ir)
    {
      this.left = ir.left; this.top = ir.top;
      this.right = ir.right; this.bottom = ir.bottom;
    }
  }

  public enum ClipType { ctIntersection, ctUnion, ctDifference, ctXor };
  public enum PolyType { ptSubject, ptClip };
  
  //By far the most widely used winding rules for polygon filling are
  //EvenOdd & NonZero (GDI, GDI+, XLib, OpenGL, Cairo, AGG, Quartz, SVG, Gr32)
  //Others rules include Positive, Negative and ABS_GTR_EQ_TWO (only in OpenGL)
  //see http://glprogramming.com/red/chapter11.html
  public enum PolyFillType { pftEvenOdd, pftNonZero, pftPositive, pftNegative };
  public enum JoinType { jtSquare, jtRound, jtMiter };
  public enum EndType { etClosed, etButt, etSquare, etRound};

  internal enum EdgeSide {esLeft, esRight};
  internal enum Direction {dRightToLeft, dLeftToRight};
    
  internal class TEdge {
      public IntPoint Bot;
      public IntPoint Curr;
      public IntPoint Top;
      public IntPoint Delta;
      public double Dx;
      public PolyType PolyTyp;
      public EdgeSide Side;
      public int WindDelta; //1 or -1 depending on winding direction
      public int WindCnt;
      public int WindCnt2; //winding count of the opposite polytype
      public int OutIdx;
      public TEdge Next;
      public TEdge Prev;
      public TEdge NextInLML;
      public TEdge NextInAEL;
      public TEdge PrevInAEL;
      public TEdge NextInSEL;
      public TEdge PrevInSEL;
  };

  internal class IntersectNode
  {
      public TEdge Edge1;
      public TEdge Edge2;
      public IntPoint Pt;
      public IntersectNode Next;
  };

  internal class LocalMinima
  {
      public cInt Y;
      public TEdge LeftBound;
      public TEdge RightBound;
      public LocalMinima Next;
  };

  internal class Scanbeam
  {
      public cInt Y;
      public Scanbeam Next;
  };

  internal class OutRec
  {
      public int Idx;
      public bool IsHole;
      public bool IsOpen;
      public OutRec FirstLeft; //see comments in clipper.pas
      public OutPt Pts;
      public OutPt BottomPt;
      public PolyNode PolyNode;
  };

  internal class OutPt
  {
      public int Idx;
      public IntPoint Pt;
      public OutPt Next;
      public OutPt Prev;
  };

  internal class Join
  {
    public OutPt OutPt1;
    public OutPt OutPt2;
    public IntPoint OffPt;
  };

  public class ClipperBase
  {    
    protected const double horizontal = -3.4E+38;
    protected const int Skip = -2;
    protected const int Unassigned = -1;
    protected const double tolerance = 1.0E-20;
    internal static bool near_zero(double val){return (val > -tolerance) && (val < tolerance);}

#if use_int32
      internal const cInt loRange = 46340;
      internal const cInt hiRange = 46340;
#else
      internal const cInt loRange = 0x3FFFFFFF;          
      internal const cInt hiRange = 0x3FFFFFFFFFFFFFFFL; 
#endif

      internal LocalMinima m_MinimaList;
      internal LocalMinima m_CurrentLM;
      internal List<List<TEdge>> m_edges = new List<List<TEdge>>();
      internal bool m_UseFullRange;
      internal bool m_HasOpenPaths;

      //------------------------------------------------------------------------------

      public bool PreserveCollinear
      {
        get;
        set;
      }
      //------------------------------------------------------------------------------

      internal static bool IsHorizontal(TEdge e)
      {
        return e.Delta.Y == 0;
      }
      //------------------------------------------------------------------------------

      internal bool PointIsVertex(IntPoint pt, OutPt pp)
      {
        OutPt pp2 = pp;
        do
        {
          if (pp2.Pt == pt) return true;
          pp2 = pp2.Next;
        }
        while (pp2 != pp);
        return false;
      }
      //------------------------------------------------------------------------------

      internal bool PointOnLineSegment(IntPoint pt, 
          IntPoint linePt1, IntPoint linePt2, bool UseFullInt64Range)
      {
        if (UseFullInt64Range)
          return ((pt.X == linePt1.X) && (pt.Y == linePt1.Y)) ||
            ((pt.X == linePt2.X) && (pt.Y == linePt2.Y)) ||
            (((pt.X > linePt1.X) == (pt.X < linePt2.X)) &&
            ((pt.Y > linePt1.Y) == (pt.Y < linePt2.Y)) &&
            ((Int128.Int128Mul((pt.X - linePt1.X), (linePt2.Y - linePt1.Y)) ==
            Int128.Int128Mul((linePt2.X - linePt1.X), (pt.Y - linePt1.Y)))));
        else
          return ((pt.X == linePt1.X) && (pt.Y == linePt1.Y)) ||
            ((pt.X == linePt2.X) && (pt.Y == linePt2.Y)) ||
            (((pt.X > linePt1.X) == (pt.X < linePt2.X)) &&
            ((pt.Y > linePt1.Y) == (pt.Y < linePt2.Y)) &&
            ((pt.X - linePt1.X) * (linePt2.Y - linePt1.Y) ==
              (linePt2.X - linePt1.X) * (pt.Y - linePt1.Y)));
      }
      //------------------------------------------------------------------------------

      internal bool PointOnPolygon(IntPoint pt, OutPt pp, bool UseFullInt64Range)
      {
        OutPt pp2 = pp;
        while (true)
        {
          if (PointOnLineSegment(pt, pp2.Pt, pp2.Next.Pt, UseFullInt64Range))
            return true;
          pp2 = pp2.Next;
          if (pp2 == pp) break;
        }
        return false;
      }
      //------------------------------------------------------------------------------

      internal bool PointInPolygon(IntPoint pt, OutPt pp, bool UseFulllongRange)
      {
        OutPt pp2 = pp;
        bool result = false;
        if (UseFulllongRange)
        {
            do
            {
                if ((((pp2.Pt.Y <= pt.Y) && (pt.Y < pp2.Prev.Pt.Y)) ||
                    ((pp2.Prev.Pt.Y <= pt.Y) && (pt.Y < pp2.Pt.Y))) &&
                    new Int128(pt.X - pp2.Pt.X) < 
                    Int128.Int128Mul(pp2.Prev.Pt.X - pp2.Pt.X,  pt.Y - pp2.Pt.Y) / 
                    new Int128(pp2.Prev.Pt.Y - pp2.Pt.Y))
                      result = !result;
                pp2 = pp2.Next;
            }
            while (pp2 != pp);
        }
        else
        {
            do
            {
                if ((((pp2.Pt.Y <= pt.Y) && (pt.Y < pp2.Prev.Pt.Y)) ||
                  ((pp2.Prev.Pt.Y <= pt.Y) && (pt.Y < pp2.Pt.Y))) &&
                  (pt.X - pp2.Pt.X < (pp2.Prev.Pt.X - pp2.Pt.X) * (pt.Y - pp2.Pt.Y) /
                  (pp2.Prev.Pt.Y - pp2.Pt.Y))) result = !result;
                pp2 = pp2.Next;
            }
            while (pp2 != pp);
        }
        return result;
      }
      //------------------------------------------------------------------------------

      internal static bool SlopesEqual(TEdge e1, TEdge e2, bool UseFullRange)
      {
          if (UseFullRange)
            return Int128.Int128Mul(e1.Delta.Y, e2.Delta.X) ==
                Int128.Int128Mul(e1.Delta.X, e2.Delta.Y);
          else return (cInt)(e1.Delta.Y) * (e2.Delta.X) ==
            (cInt)(e1.Delta.X) * (e2.Delta.Y);
      }
      //------------------------------------------------------------------------------

      protected static bool SlopesEqual(IntPoint pt1, IntPoint pt2,
          IntPoint pt3, bool UseFullRange)
      {
          if (UseFullRange)
              return Int128.Int128Mul(pt1.Y - pt2.Y, pt2.X - pt3.X) ==
                Int128.Int128Mul(pt1.X - pt2.X, pt2.Y - pt3.Y);
          else return
            (cInt)(pt1.Y - pt2.Y) * (pt2.X - pt3.X) - (cInt)(pt1.X - pt2.X) * (pt2.Y - pt3.Y) == 0;
      }
      //------------------------------------------------------------------------------

      protected static bool SlopesEqual(IntPoint pt1, IntPoint pt2,
          IntPoint pt3, IntPoint pt4, bool UseFullRange)
      {
          if (UseFullRange)
              return Int128.Int128Mul(pt1.Y - pt2.Y, pt3.X - pt4.X) ==
                Int128.Int128Mul(pt1.X - pt2.X, pt3.Y - pt4.Y);
          else return
            (cInt)(pt1.Y - pt2.Y) * (pt3.X - pt4.X) - (cInt)(pt1.X - pt2.X) * (pt3.Y - pt4.Y) == 0;
      }
      //------------------------------------------------------------------------------

      internal ClipperBase() //constructor (nb: no external instantiation)
      {
          m_MinimaList = null;
          m_CurrentLM = null;
          m_UseFullRange = false;
          m_HasOpenPaths = false;
      }
      //------------------------------------------------------------------------------

      public virtual void Clear()
      {
          DisposeLocalMinimaList();
          for (int i = 0; i < m_edges.Count; ++i)
          {
              for (int j = 0; j < m_edges[i].Count; ++j) m_edges[i][j] = null;
              m_edges[i].Clear();
          }
          m_edges.Clear();
          m_UseFullRange = false;
          m_HasOpenPaths = false;
      }
      //------------------------------------------------------------------------------

      private void DisposeLocalMinimaList()
      {
          while( m_MinimaList != null )
          {
              LocalMinima tmpLm = m_MinimaList.Next;
              m_MinimaList = null;
              m_MinimaList = tmpLm;
          }
          m_CurrentLM = null;
      }
      //------------------------------------------------------------------------------

      void RangeTest(IntPoint Pt, ref bool useFullRange)
      {
        if (useFullRange)
        {
          if (Pt.X > hiRange || Pt.Y > hiRange || -Pt.X > hiRange || -Pt.Y > hiRange) 
            throw new ClipperException("Coordinate outside allowed range");
        }
        else if (Pt.X > loRange || Pt.Y > loRange || -Pt.X > loRange || -Pt.Y > loRange) 
        {
          useFullRange = true;
          RangeTest(Pt, ref useFullRange);
        }
      }
      //------------------------------------------------------------------------------

      private void InitEdge(TEdge e, TEdge eNext,
        TEdge ePrev, IntPoint pt)
      {
        e.Next = eNext;
        e.Prev = ePrev;
        e.Curr = pt;
        e.OutIdx = Unassigned;
      }
      //------------------------------------------------------------------------------

      private void InitEdge2(TEdge e, PolyType polyType)
      {
        if (e.Curr.Y >= e.Next.Curr.Y)
        {
          e.Bot = e.Curr;
          e.Top = e.Next.Curr;
        }
        else
        {
          e.Top = e.Curr;
          e.Bot = e.Next.Curr;
        }
        SetDx(e);
        e.PolyTyp = polyType;
      }
      //------------------------------------------------------------------------------

      public bool AddPath(Path pg, PolyType polyType, bool Closed)
      {
        int highI = (int)pg.Count -1;
        while (highI > 0 && (pg[highI] == pg[highI -1])) highI--;
        if (highI < 1) return false;

#if use_lines
        if (!Closed && polyType == PolyType.ptClip)
          throw new ClipperException("AddPath: Open paths must be subject.");
#else
        if (!Closed)
          throw new ClipperException("AddPath: Open paths have been disabled.");
#endif

        bool ClosedOrSemiClosed = (Closed || (pg[0] == pg[highI]));
        while (highI > 0 && (pg[highI] == pg[highI - 1])) --highI;
        if (highI > 0 && (pg[0] == pg[highI])) --highI;
        if ((Closed && highI < 2) || (!Closed && highI < 1)) return false;

          //create a new edge array ...
          List<TEdge> edges = new List<TEdge>(highI+1);
          for (int i = 0; i <= highI; i++) edges.Add(new TEdge());
          
          //1. Basic initialization of Edges ...
          try
          {
            edges[1].Curr = pg[1];
            RangeTest(pg[0], ref m_UseFullRange);
            RangeTest(pg[highI], ref m_UseFullRange);
            InitEdge(edges[0], edges[1], edges[highI], pg[0]);
            InitEdge(edges[highI], edges[0], edges[highI - 1], pg[highI]);
            for (int i = highI - 1; i >= 1; --i)
            {
              RangeTest(pg[i], ref m_UseFullRange);
              InitEdge(edges[i], edges[i + 1], edges[i - 1], pg[i]);
            }
          }
          catch 
          {
            return false; //almost certainly a vertex has exceeded range
          };

          TEdge eStart = edges[0];
          if (!ClosedOrSemiClosed) eStart.Prev.OutIdx = Skip;

          //2. Remove duplicate vertices, and collinear edges (when closed) ...
          TEdge E = eStart, eLoopStop = eStart;
          for (;;)
          {
            if (E.Curr == E.Next.Curr)
            {
              //nb if E.OutIdx == Skip, it would have been semiOpen
              if (E == eStart) eStart = E.Next;
              E = RemoveEdge(E);
              eLoopStop = E;
              continue;
            }
            if (E.Prev == E.Next) 
              break; //only two vertices
            else if ((ClosedOrSemiClosed ||
              (E.Prev.OutIdx != Skip && E.OutIdx != Skip &&
              E.Next.OutIdx != Skip)) &&
              SlopesEqual(E.Prev.Curr, E.Curr, E.Next.Curr, m_UseFullRange)) 
            {
              //All collinear edges are allowed for open paths but in closed paths
              //inner vertices of adjacent collinear edges are removed. However if the
              //PreserveCollinear property has been enabled, only overlapping collinear
              //edges (ie spikes) are removed from closed paths.
              if (Closed && (!PreserveCollinear ||
                !Pt2IsBetweenPt1AndPt3(E.Prev.Curr, E.Curr, E.Next.Curr))) 
              {
                if (E == eStart) eStart = E.Next;
                E = RemoveEdge(E);
                E = E.Prev;
                eLoopStop = E;
                continue;
              }
            }
            E = E.Next;
            if (E == eLoopStop) break;
          }

          if ((!Closed && (E == E.Next)) || (Closed && (E.Prev == E.Next)))
            return false;
          m_edges.Add(edges);

          if (!Closed)
            m_HasOpenPaths = true;

          //3. Do final Init and also find the 'highest' Edge. (nb: since I'm much
          //more familiar with positive downwards Y axes, 'highest' here will be
          //the Edge with the *smallest* Top.Y.)
          TEdge eHighest = eStart;
          E = eStart;
          do
          {
            InitEdge2(E, polyType);
            if (E.Top.Y < eHighest.Top.Y) eHighest = E;
            E = E.Next;
          }
          while (E != eStart);

          //4. build the local minima list ...
          if (AllHorizontal(E))
          {
            if (ClosedOrSemiClosed)
              E.Prev.OutIdx = Skip;
            AscendToMax(ref E, false, false);
            return true;
          }

          //if eHighest is also the Skip then it's a natural break, otherwise
          //make sure eHighest is positioned so we're either at a top horizontal or
          //just starting to head down one edge of the polygon
          E = eStart.Prev; //EStart.Prev == Skip edge
          if (E.Prev == E.Next)
            eHighest = E.Next;
          else if (!ClosedOrSemiClosed && E.Top.Y == eHighest.Top.Y)
          {
            if ((IsHorizontal(E) || IsHorizontal(E.Next)) && 
              E.Next.Bot.Y == eHighest.Top.Y)
                eHighest = E.Next;
            else if (SharedVertWithPrevAtTop(E)) eHighest = E;
            else if (E.Top == E.Prev.Top) eHighest = E.Prev;
            else eHighest = E.Next;
          } else
          {
            E = eHighest;
            while (IsHorizontal(eHighest) ||
              (eHighest.Top == eHighest.Next.Top) ||
              (eHighest.Top == eHighest.Next.Bot)) //next is high horizontal
            {
              eHighest = eHighest.Next;
              if (eHighest == E) 
              {
                while (IsHorizontal(eHighest) || !SharedVertWithPrevAtTop(eHighest))
                    eHighest = eHighest.Next;
                break; //avoids potential endless loop
              }
            }
          }
          E = eHighest;
          do
            E = AddBoundsToLML(E, Closed);
          while (E != eHighest);
          return true;
      }
      //------------------------------------------------------------------------------

      public bool AddPaths(Paths ppg, PolyType polyType, bool closed)
      {
        bool result = false;
        for (int i = 0; i < ppg.Count; ++i)
          if (AddPath(ppg[i], polyType, closed)) result = true;
        return result;
      }
      //------------------------------------------------------------------------------

#if use_deprecated
      public bool AddPolygon(Path pg, PolyType polyType)
      {
        return AddPath(pg, polyType, true);
      }
      //------------------------------------------------------------------------------

      public bool AddPolygons(Paths ppg, PolyType polyType)
      {
        bool result = false;
        for (int i = 0; i < ppg.Count; ++i)
          if (AddPath(ppg[i], polyType, true)) result = true;
        return result;
      }
      //------------------------------------------------------------------------------
#endif

      internal bool Pt2IsBetweenPt1AndPt3(IntPoint pt1, IntPoint pt2, IntPoint pt3)
      {
        if ((pt1 == pt3) || (pt1 == pt2) || (pt3 == pt2)) return false;
        else if (pt1.X != pt3.X) return (pt2.X > pt1.X) == (pt2.X < pt3.X);
        else return (pt2.Y > pt1.Y) == (pt2.Y < pt3.Y);
      }
      //------------------------------------------------------------------------------

      TEdge RemoveEdge(TEdge e)
      {
        //removes e from double_linked_list (but without removing from memory)
        e.Prev.Next = e.Next;
        e.Next.Prev = e.Prev;
        TEdge result = e.Next;
        e.Prev = null; //flag as removed (see ClipperBase.Clear)
        return result;
      }
      //------------------------------------------------------------------------------

      TEdge GetLastHorz(TEdge Edge)
      {
        TEdge result = Edge;
        while (result.OutIdx != Skip && result.Next != Edge && IsHorizontal(result.Next))
          result = result.Next;
        return result;
      }
      //------------------------------------------------------------------------------

      bool SharedVertWithPrevAtTop(TEdge Edge)
      {
        TEdge E = Edge;
        bool result = true;
        while (E.Prev != Edge)
        {
          if (E.Top == E.Prev.Top)
          {
            if (E.Bot == E.Prev.Bot)
            {E = E.Prev; continue;}
            else result = true;
          }
          else result = false;
          break;
        }
        while (E != Edge)
        {
          result = !result;
          E = E.Next;
        }
        return result;
      }
      //------------------------------------------------------------------------------

      bool SharedVertWithNextIsBot(TEdge Edge)
      {
        bool result = true;
        TEdge E = Edge;
        while (E.Prev != Edge)
        {
          bool A = (E.Next.Bot == E.Bot);
          bool B = (E.Prev.Bot == E.Bot);
          if (A != B)
          {
            result = A;
            break;
          }
          A = (E.Next.Top == E.Top);
          B = (E.Prev.Top == E.Top);
          if (A != B)
          {
            result = B;
            break;
          }
          E = E.Prev;
        }
        while (E != Edge)
        {
          result = !result;
          E = E.Next;
        }
        return result;
      }
      //------------------------------------------------------------------------------

      bool MoreBelow(TEdge Edge)
      {
        //Edge is Skip heading down.
        TEdge E = Edge;
        if (IsHorizontal(E))
        {
          while (IsHorizontal(E.Next)) E = E.Next;
          return E.Next.Bot.Y > E.Bot.Y;
        }
        else if (IsHorizontal(E.Next))
        {
          while (IsHorizontal(E.Next)) E = E.Next;
          return E.Next.Bot.Y > E.Bot.Y;
        }
        else return (E.Bot == E.Next.Top);
      }
      //------------------------------------------------------------------------------

      bool JustBeforeLocMin(TEdge Edge)
      {
        //Edge is Skip and was heading down.
        TEdge E = Edge;
        if (IsHorizontal(E))
        {
          while (IsHorizontal(E.Next)) E = E.Next;
          return E.Next.Top.Y < E.Bot.Y;
        }
        else return SharedVertWithNextIsBot(E);
      }
      //------------------------------------------------------------------------------

      bool MoreAbove(TEdge Edge)
      {
        if (IsHorizontal(Edge))
        {
          Edge = GetLastHorz(Edge);
          return (Edge.Next.Top.Y < Edge.Top.Y);
        }
        else if (IsHorizontal(Edge.Next))
        {
          Edge = GetLastHorz(Edge.Next);
          return (Edge.Next.Top.Y < Edge.Top.Y);
        }
        else
          return (Edge.Next.Top.Y < Edge.Top.Y);
      }
      //------------------------------------------------------------------------------

      bool AllHorizontal(TEdge Edge)
      {
        if (!IsHorizontal(Edge)) return false;
        TEdge E = Edge.Next;
        while (E != Edge)
        {
          if (!IsHorizontal(E)) return false;
          else E = E.Next;
        }
        return true;
      }
      //------------------------------------------------------------------------------

      private void SetDx(TEdge e)
      {
        e.Delta.X = (e.Top.X - e.Bot.X);
        e.Delta.Y = (e.Top.Y - e.Bot.Y);
        if (e.Delta.Y == 0) e.Dx = horizontal;
        else e.Dx = (double)(e.Delta.X) / (e.Delta.Y);
      }
      //---------------------------------------------------------------------------

      void DoMinimaLML(TEdge E1, TEdge E2, bool IsClosed)
      {
        if (E1 == null)
        {
          if (E2 == null) return;
          LocalMinima NewLm = new LocalMinima();
          NewLm.Next = null;
          NewLm.Y = E2.Bot.Y;
          NewLm.LeftBound = null;
          E2.WindDelta = 0;
          NewLm.RightBound = E2;
          InsertLocalMinima(NewLm);
        } else
        {
          //E and E.Prev are now at a local minima ...
          LocalMinima NewLm = new LocalMinima();
          NewLm.Y = E1.Bot.Y;
          NewLm.Next = null;
          if (IsHorizontal(E2)) //Horz. edges never start a Left bound
          {
            if (E2.Bot.X != E1.Bot.X) ReverseHorizontal(E2);
            NewLm.LeftBound = E1;
            NewLm.RightBound = E2;
          } else if (E2.Dx < E1.Dx)
          {
            NewLm.LeftBound = E1;
            NewLm.RightBound = E2;
          } else
          {
            NewLm.LeftBound = E2;
            NewLm.RightBound = E1;
          }
          NewLm.LeftBound.Side = EdgeSide.esLeft;
          NewLm.RightBound.Side = EdgeSide.esRight;
          //set the winding state of the first edge in each bound
          //(it'll be copied to subsequent edges in the bound) ...
          if (!IsClosed) NewLm.LeftBound.WindDelta = 0;
          else if (NewLm.LeftBound.Next == NewLm.RightBound) NewLm.LeftBound.WindDelta = -1;
          else NewLm.LeftBound.WindDelta = 1;
          NewLm.RightBound.WindDelta = -NewLm.LeftBound.WindDelta;
          InsertLocalMinima(NewLm);
        }
      }
      //----------------------------------------------------------------------

      TEdge DescendToMin(ref TEdge E)
      {
        //PRECONDITION: STARTING EDGE IS A VALID DESCENDING EDGE.
        //Starting at the top of one bound we progress to the bottom where there's
        //A local minima. We  go to the top of the Next bound. These two bounds
        //form the left and right (or right and left) bounds of the local minima.
        TEdge EHorz;
        E.NextInLML = null;
        if (IsHorizontal(E)) 
        {
          EHorz = E;
          while (IsHorizontal(EHorz.Next)) EHorz = EHorz.Next;
          if (EHorz.Bot!= EHorz.Next.Top)
            ReverseHorizontal(E);
        }
        for (;;)
        {
          E = E.Next;
          if (E.OutIdx == Skip) break;
          else if (IsHorizontal(E))
          {
            //nb: proceed through horizontals when approaching from their right,
            //    but break on horizontal minima if approaching from their left.
            //    This ensures 'local minima' are always on the left of horizontals.

            //look ahead is required in case of multiple consec. horizontals
            EHorz = GetLastHorz(E);
            if(EHorz == E.Prev ||                    //horizontal line
              (EHorz.Next.Top.Y < E.Top.Y &&      //bottom horizontal
              EHorz.Next.Bot.X > E.Prev.Bot.X))  //approaching from the left
                break;
            if (E.Top.X != E.Prev.Bot.X)  ReverseHorizontal(E);
            if (EHorz.OutIdx == Skip) EHorz = EHorz.Prev;
            while (E != EHorz)
            {
              E.NextInLML = E.Prev;
              E = E.Next;
              if (E.Top.X != E.Prev.Bot.X) ReverseHorizontal(E);
            }
          }
          else if (E.Bot.Y == E.Prev.Bot.Y)  break;
          E.NextInLML = E.Prev;
        }
        return E.Prev;
      }
      //----------------------------------------------------------------------

      void AscendToMax(ref TEdge E, bool Appending, bool IsClosed)
      {
        if (E.OutIdx == Skip)
        {
          E = E.Next;
          if (!MoreAbove(E.Prev)) return;
        }

        if (IsHorizontal(E) && Appending &&
          (E.Bot != E.Prev.Bot))
            ReverseHorizontal(E);
        //now process the ascending bound ....
        TEdge EStart = E;
        for (;;)
        {
          if (E.Next.OutIdx == Skip ||
            ((E.Next.Top.Y == E.Top.Y) && !IsHorizontal(E.Next))) break;
          E.NextInLML = E.Next;
          E = E.Next;
          if (IsHorizontal(E) && (E.Bot.X != E.Prev.Top.X))
            ReverseHorizontal(E);
        }

        if (!Appending)
        {
          if (EStart.OutIdx == Skip) EStart = EStart.Next;
          if (EStart != E.Next)
            DoMinimaLML(null, EStart, IsClosed);
        }
        E = E.Next;
      }
      //----------------------------------------------------------------------

      TEdge AddBoundsToLML(TEdge E, bool Closed)
      {
        //Starting at the top of one bound we progress to the bottom where there's
        //A local minima. We then go to the top of the Next bound. These two bounds
        //form the left and right (or right and left) bounds of the local minima.

        TEdge B;
        bool AppendMaxima;
        //do minima ...
        if (E.OutIdx == Skip)
        {
          if (MoreBelow(E))
          {
            E = E.Next;
            B = DescendToMin(ref E);
          }
          else
            B = null;
        }
        else
          B = DescendToMin(ref E);

        if (E.OutIdx == Skip)    //nb: may be BEFORE, AT or just THRU LM
        {
          //do minima before Skip...
          DoMinimaLML(null, B, Closed);      //store what we've got so far (if anything)
          AppendMaxima = false;
          //finish off any minima ...
          if (E.Bot != E.Prev.Bot && MoreBelow(E))
          {
            E = E.Next;
            B = DescendToMin(ref E);
            DoMinimaLML(B, E, Closed);
            AppendMaxima = true;
          }
          else if (JustBeforeLocMin(E))
            E = E.Next;
        }
        else
        {
          DoMinimaLML(B, E, Closed);
          AppendMaxima = true;
        }

        //now do maxima ...
        AscendToMax(ref E, AppendMaxima, Closed);

        if (E.OutIdx == Skip && (E.Top != E.Prev.Top)) //may be BEFORE, AT or just AFTER maxima
        {
          //finish off any maxima ...
          if (MoreAbove(E))
          {
            E = E.Next;
            AscendToMax(ref E, false, Closed);
          }
          else if (E.Top == E.Next.Top || (IsHorizontal(E.Next) && (E.Top == E.Next.Bot)))
            E = E.Next; //ie just before Maxima
        }
        return E;
      }
      //------------------------------------------------------------------------------

      private void InsertLocalMinima(LocalMinima newLm)
      {
        if( m_MinimaList == null )
        {
          m_MinimaList = newLm;
        }
        else if( newLm.Y >= m_MinimaList.Y )
        {
          newLm.Next = m_MinimaList;
          m_MinimaList = newLm;
        } else
        {
          LocalMinima tmpLm = m_MinimaList;
          while( tmpLm.Next != null  && ( newLm.Y < tmpLm.Next.Y ) )
            tmpLm = tmpLm.Next;
          newLm.Next = tmpLm.Next;
          tmpLm.Next = newLm;
        }
      }
      //------------------------------------------------------------------------------

      protected void PopLocalMinima()
      {
          if (m_CurrentLM == null) return;
          m_CurrentLM = m_CurrentLM.Next;
      }
      //------------------------------------------------------------------------------

      private void ReverseHorizontal(TEdge e)
      {
        //swap horizontal edges' top and bottom x's so they follow the natural
        //progression of the bounds - ie so their xbots will align with the
        //adjoining lower edge. [Helpful in the ProcessHorizontal() method.]
        cInt tmp = e.Top.X;
        e.Top.X = e.Bot.X;
        e.Bot.X = tmp;
#if use_xyz
        tmp = e.Top.Z;
        e.Top.Z = e.Bot.Z;
        e.Bot.Z = tmp;
#endif
      }
      //------------------------------------------------------------------------------

      protected virtual void Reset()
      {
        m_CurrentLM = m_MinimaList;
        if (m_CurrentLM == null) return; //ie nothing to process

        //reset all edges ...
        LocalMinima lm = m_MinimaList;
        while (lm != null)
        {
          TEdge e = lm.LeftBound;
          if (e != null)
          {
            e.Curr = e.Bot;
            e.Side = EdgeSide.esLeft;
            if (e.OutIdx != Skip)
              e.OutIdx = Unassigned;
          }
          e = lm.RightBound;
          e.Curr = e.Bot;
          e.Side = EdgeSide.esRight;
          if (e.OutIdx != Skip)
            e.OutIdx = Unassigned;

          lm = lm.Next;
        }
      }
      //------------------------------------------------------------------------------

      public IntRect GetBounds()
      {
          IntRect result = new IntRect();
          LocalMinima lm = m_MinimaList;
          if (lm == null) return result;
          result.left = lm.LeftBound.Bot.X;
          result.top = lm.LeftBound.Bot.Y;
          result.right = lm.LeftBound.Bot.X;
          result.bottom = lm.LeftBound.Bot.Y;
          while (lm != null)
          {
              if (lm.LeftBound.Bot.Y > result.bottom)
                  result.bottom = lm.LeftBound.Bot.Y;
              TEdge e = lm.LeftBound;
              for (; ; )
              {
                  TEdge bottomE = e;
                  while (e.NextInLML != null)
                  {
                      if (e.Bot.X < result.left) result.left = e.Bot.X;
                      if (e.Bot.X > result.right) result.right = e.Bot.X;
                      e = e.NextInLML;
                  }
                  if (e.Bot.X < result.left) result.left = e.Bot.X;
                  if (e.Bot.X > result.right) result.right = e.Bot.X;
                  if (e.Top.X < result.left) result.left = e.Top.X;
                  if (e.Top.X > result.right) result.right = e.Top.X;
                  if (e.Top.Y < result.top) result.top = e.Top.Y;

                  if (bottomE == lm.LeftBound) e = lm.RightBound;
                  else break;
              }
              lm = lm.Next;
          }
          return result;
      }

  } //ClipperBase

  public class Clipper : ClipperBase
  {
      //InitOptions that can be passed to the constructor ...
      public const int ioReverseSolution = 1;
      public const int ioStrictlySimple = 2;
      public const int ioPreserveCollinear = 4;

      private List<OutRec> m_PolyOuts;
      private ClipType m_ClipType;
      private Scanbeam m_Scanbeam;
      private TEdge m_ActiveEdges;
      private TEdge m_SortedEdges;
      private IntersectNode m_IntersectNodes;
      private bool m_ExecuteLocked;
      private PolyFillType m_ClipFillType;
      private PolyFillType m_SubjFillType;
      private List<Join> m_Joins;
      private List<Join> m_GhostJoins;
      private bool m_UsingPolyTree;
#if use_xyz
      public delegate void TZFillCallback(cInt Z1, cInt Z2, ref IntPoint pt);
      public TZFillCallback ZFillFunction { get; set; }
#endif
      public Clipper(int InitOptions = 0): base() //constructor
      {
          m_Scanbeam = null;
          m_ActiveEdges = null;
          m_SortedEdges = null;
          m_IntersectNodes = null;
          m_ExecuteLocked = false;
          m_UsingPolyTree = false;
          m_PolyOuts = new List<OutRec>();
          m_Joins = new List<Join>();
          m_GhostJoins = new List<Join>();
          ReverseSolution = (ioReverseSolution & InitOptions) != 0;
          StrictlySimple = (ioStrictlySimple & InitOptions) != 0;
          PreserveCollinear = (ioPreserveCollinear & InitOptions) != 0;
#if use_xyz
          ZFillFunction = null;
#endif
      }
      //------------------------------------------------------------------------------

      public override void Clear()
      {
          if (m_edges.Count == 0) return; //avoids problems with ClipperBase destructor
          DisposeAllPolyPts();
          base.Clear();
      }
      //------------------------------------------------------------------------------

      void DisposeScanbeamList()
      {
        while ( m_Scanbeam != null ) {
        Scanbeam sb2 = m_Scanbeam.Next;
        m_Scanbeam = null;
        m_Scanbeam = sb2;
        }
      }
      //------------------------------------------------------------------------------

      protected override void Reset() 
      {
        base.Reset();
        m_Scanbeam = null;
        m_ActiveEdges = null;
        m_SortedEdges = null;
        DisposeAllPolyPts();
        LocalMinima lm = m_MinimaList;
        while (lm != null)
        {
          InsertScanbeam(lm.Y);
          lm = lm.Next;
        }
      }
      //------------------------------------------------------------------------------

      public bool ReverseSolution
      {
        get;
        set;
      }
      //------------------------------------------------------------------------------

      public bool StrictlySimple
      {
        get; 
        set;
      }
      //------------------------------------------------------------------------------
       
      private void InsertScanbeam(cInt Y)
      {
        if( m_Scanbeam == null )
        {
          m_Scanbeam = new Scanbeam();
          m_Scanbeam.Next = null;
          m_Scanbeam.Y = Y;
        }
        else if(  Y > m_Scanbeam.Y )
        {
          Scanbeam newSb = new Scanbeam();
          newSb.Y = Y;
          newSb.Next = m_Scanbeam;
          m_Scanbeam = newSb;
        } else
        {
          Scanbeam sb2 = m_Scanbeam;
          while( sb2.Next != null  && ( Y <= sb2.Next.Y ) ) sb2 = sb2.Next;
          if(  Y == sb2.Y ) return; //ie ignores duplicates
          Scanbeam newSb = new Scanbeam();
          newSb.Y = Y;
          newSb.Next = sb2.Next;
          sb2.Next = newSb;
        }
      }
      //------------------------------------------------------------------------------

      public bool Execute(ClipType clipType, Paths solution,
          PolyFillType subjFillType, PolyFillType clipFillType)
      {
          if (m_ExecuteLocked) return false;
          if (m_HasOpenPaths) throw 
            new ClipperException("Error: PolyTree struct is need for open path clipping.");

        m_ExecuteLocked = true;
          solution.Clear();
          m_SubjFillType = subjFillType;
          m_ClipFillType = clipFillType;
          m_ClipType = clipType;
          m_UsingPolyTree = false;
          bool succeeded = ExecuteInternal();
          //build the return polygons ...
          if (succeeded) BuildResult(solution);
          m_ExecuteLocked = false;
          return succeeded;
      }
      //------------------------------------------------------------------------------

      public bool Execute(ClipType clipType, PolyTree polytree,
          PolyFillType subjFillType, PolyFillType clipFillType)
      {
          if (m_ExecuteLocked) return false;
          m_ExecuteLocked = true;
          m_SubjFillType = subjFillType;
          m_ClipFillType = clipFillType;
          m_ClipType = clipType;
          m_UsingPolyTree = true;
          bool succeeded = ExecuteInternal();
          //build the return polygons ...
          if (succeeded) BuildResult2(polytree);
          m_ExecuteLocked = false;
          return succeeded;
      }
      //------------------------------------------------------------------------------

      public bool Execute(ClipType clipType, Paths solution)
      {
          return Execute(clipType, solution,
              PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
      }
      //------------------------------------------------------------------------------

      public bool Execute(ClipType clipType, PolyTree polytree)
      {
          return Execute(clipType, polytree,
              PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
      }
      //------------------------------------------------------------------------------

      internal void FixHoleLinkage(OutRec outRec)
      {
          //skip if an outermost polygon or
          //already already points to the correct FirstLeft ...
          if (outRec.FirstLeft == null ||                
                (outRec.IsHole != outRec.FirstLeft.IsHole &&
                outRec.FirstLeft.Pts != null)) return;

          OutRec orfl = outRec.FirstLeft;
          while (orfl != null && ((orfl.IsHole == outRec.IsHole) || orfl.Pts == null))
              orfl = orfl.FirstLeft;
          outRec.FirstLeft = orfl;
      }
      //------------------------------------------------------------------------------

      private bool ExecuteInternal()
      {
        try
        {
          Reset();
          if (m_CurrentLM == null) return false;

          cInt botY = PopScanbeam();
          do
          {
            InsertLocalMinimaIntoAEL(botY);
            m_GhostJoins.Clear();
            ProcessHorizontals(false);
            if (m_Scanbeam == null) break;
            cInt topY = PopScanbeam();
            if (!ProcessIntersections(botY, topY)) return false;
            ProcessEdgesAtTopOfScanbeam(topY);
            botY = topY;
          } while (m_Scanbeam != null || m_CurrentLM != null);

          //fix orientations ...
          for (int i = 0; i < m_PolyOuts.Count; i++)
          {
            OutRec outRec = m_PolyOuts[i];
            if (outRec.Pts == null || outRec.IsOpen) continue;
            if ((outRec.IsHole ^ ReverseSolution) == (Area(outRec) > 0))
              ReversePolyPtLinks(outRec.Pts);
          }

          JoinCommonEdges();

          for (int i = 0; i < m_PolyOuts.Count; i++)
          {
            OutRec outRec = m_PolyOuts[i];
            if (outRec.Pts != null && !outRec.IsOpen) 
              FixupOutPolygon(outRec);
          }

          if (StrictlySimple) DoSimplePolygons();
          return true;
        }
        //catch { return false; }
        finally 
        {
          m_Joins.Clear();
          m_GhostJoins.Clear();          
        }
      }
      //------------------------------------------------------------------------------

      private cInt PopScanbeam()
      {
        cInt Y = m_Scanbeam.Y;
        Scanbeam sb2 = m_Scanbeam;
        m_Scanbeam = m_Scanbeam.Next;
        sb2 = null;
        return Y;
      }
      //------------------------------------------------------------------------------

      private void DisposeAllPolyPts(){
        for (int i = 0; i < m_PolyOuts.Count; ++i) DisposeOutRec(i);
        m_PolyOuts.Clear();
      }
      //------------------------------------------------------------------------------

      void DisposeOutRec(int index)
      {
        OutRec outRec = m_PolyOuts[index];
        if (outRec.Pts != null) DisposeOutPts(outRec.Pts);
        outRec = null;
        m_PolyOuts[index] = null;
      }
      //------------------------------------------------------------------------------

      private void DisposeOutPts(OutPt pp)
      {
          if (pp == null) return;
          OutPt tmpPp = null;
          pp.Prev.Next = null;
          while (pp != null)
          {
              tmpPp = pp;
              pp = pp.Next;
              tmpPp = null;
          }
      }
      //------------------------------------------------------------------------------

      private void AddJoin(OutPt Op1, OutPt Op2, IntPoint OffPt)
      {
        Join j = new Join();
        j.OutPt1 = Op1;
        j.OutPt2 = Op2;
        j.OffPt = OffPt;
        m_Joins.Add(j);
      }
      //------------------------------------------------------------------------------

      private void AddGhostJoin(OutPt Op, IntPoint OffPt)
      {
        Join j = new Join();
        j.OutPt1 = Op;
        j.OffPt = OffPt;
        m_GhostJoins.Add(j);
      }
      //------------------------------------------------------------------------------

#if use_xyz
      void GetZ(ref IntPoint pt, TEdge e)
      {
        if (pt == e.Bot) pt.Z = e.Bot.Z;
        else if (pt == e.Top) pt.Z = e.Top.Z;
        else if (e.WindDelta > 0) pt.Z = e.Bot.Z;
        else pt.Z = e.Top.Z;
      }
      //------------------------------------------------------------------------------

      internal void SetZ(ref IntPoint pt, TEdge e1, TEdge e2)
      {
        pt.Z = 0;
        if (ZFillFunction != null)
        {
          IntPoint pt1 = new IntPoint(pt);
          IntPoint pt2 = new IntPoint(pt);
          GetZ(ref pt1, e1);
          GetZ(ref pt2, e2);
          ZFillFunction(pt1.Z, pt2.Z, ref pt);
        }
      }
      //------------------------------------------------------------------------------
#endif

      private void InsertLocalMinimaIntoAEL(cInt botY)
      {
        while(  m_CurrentLM != null  && ( m_CurrentLM.Y == botY ) )
        {
          TEdge lb = m_CurrentLM.LeftBound;
          TEdge rb = m_CurrentLM.RightBound;
          PopLocalMinima();

          OutPt Op1 = null;
          if (lb == null)
          {
            InsertEdgeIntoAEL(rb, null);
            SetWindingCount(rb);
            if (IsContributing(rb))
              Op1 = AddOutPt(rb, rb.Bot);
          }
          else
          {
            InsertEdgeIntoAEL(lb, null);
            InsertEdgeIntoAEL(rb, lb);
            SetWindingCount(lb);
            rb.WindCnt = lb.WindCnt;
            rb.WindCnt2 = lb.WindCnt2;
            if (IsContributing(lb))
              Op1 = AddLocalMinPoly(lb, rb, lb.Bot);

            InsertScanbeam(lb.Top.Y);
          }

          if (IsHorizontal(rb))
            AddEdgeToSEL(rb);
          else
            InsertScanbeam( rb.Top.Y );

          if (lb == null) continue;

          //if output polygons share an Edge with a horizontal rb, they'll need joining later ...
          if (Op1 != null && IsHorizontal(rb) && 
            m_GhostJoins.Count > 0 && rb.WindDelta != 0)
          {
            for (int i = 0; i < m_GhostJoins.Count; i++)
            {
              //if the horizontal Rb and a 'ghost' horizontal overlap, then convert
              //the 'ghost' join to a real join ready for later ...
              Join j = m_GhostJoins[i];
              if (HorzSegmentsOverlap(j.OutPt1.Pt, j.OffPt, rb.Bot, rb.Top))
                AddJoin(j.OutPt1, Op1, j.OffPt);
            }
          }

          if (lb.OutIdx >= 0 && lb.PrevInAEL != null &&
            lb.PrevInAEL.Curr.X == lb.Bot.X &&
            lb.PrevInAEL.OutIdx >= 0 &&
            SlopesEqual(lb.PrevInAEL, lb, m_UseFullRange) &&
            lb.WindDelta != 0 && lb.PrevInAEL.WindDelta != 0)
          {
            OutPt Op2 = AddOutPt(lb.PrevInAEL, lb.Bot);
            AddJoin(Op1, Op2, lb.Top);
          }

          if( lb.NextInAEL != rb )
          {

            if (rb.OutIdx >= 0 && rb.PrevInAEL.OutIdx >= 0 &&
              SlopesEqual(rb.PrevInAEL, rb, m_UseFullRange) &&
              rb.WindDelta != 0 && rb.PrevInAEL.WindDelta != 0)
            {
              OutPt Op2 = AddOutPt(rb.PrevInAEL, rb.Bot);
              AddJoin(Op1, Op2, rb.Top);
            }

            TEdge e = lb.NextInAEL;
            IntPoint Pt = lb.Curr;
            if (e != null)
              while (e != rb)
              {
                //nb: For calculating winding counts etc, IntersectEdges() assumes
                //that param1 will be to the right of param2 ABOVE the intersection ...
#if use_xyz
                SetZ(ref Pt, rb, e);
#endif
                IntersectEdges(rb, e, Pt); //order important here
                e = e.NextInAEL;
              }
          }
        }
      }
      //------------------------------------------------------------------------------

      private void InsertEdgeIntoAEL(TEdge edge, TEdge startEdge)
      {
        if (m_ActiveEdges == null)
        {
          edge.PrevInAEL = null;
          edge.NextInAEL = null;
          m_ActiveEdges = edge;
        }
        else if (startEdge == null && E2InsertsBeforeE1(m_ActiveEdges, edge))
        {
          edge.PrevInAEL = null;
          edge.NextInAEL = m_ActiveEdges;
          m_ActiveEdges.PrevInAEL = edge;
          m_ActiveEdges = edge;
        }
        else
        {
          if (startEdge == null) startEdge = m_ActiveEdges;
          while (startEdge.NextInAEL != null &&
            !E2InsertsBeforeE1(startEdge.NextInAEL, edge))
            startEdge = startEdge.NextInAEL;
          edge.NextInAEL = startEdge.NextInAEL;
          if (startEdge.NextInAEL != null) startEdge.NextInAEL.PrevInAEL = edge;
          edge.PrevInAEL = startEdge;
          startEdge.NextInAEL = edge;
        }
      }
      //----------------------------------------------------------------------

      private bool E2InsertsBeforeE1(TEdge e1, TEdge e2)
      {
          if (e2.Curr.X == e1.Curr.X)
          {
              if (e2.Top.Y > e1.Top.Y)
                  return e2.Top.X < TopX(e1, e2.Top.Y);
              else return e1.Top.X > TopX(e2, e1.Top.Y);
          }
          else return e2.Curr.X < e1.Curr.X;
      }
      //------------------------------------------------------------------------------

      private bool IsEvenOddFillType(TEdge edge) 
      {
        if (edge.PolyTyp == PolyType.ptSubject)
            return m_SubjFillType == PolyFillType.pftEvenOdd; 
        else
            return m_ClipFillType == PolyFillType.pftEvenOdd;
      }
      //------------------------------------------------------------------------------

      private bool IsEvenOddAltFillType(TEdge edge) 
      {
        if (edge.PolyTyp == PolyType.ptSubject)
            return m_ClipFillType == PolyFillType.pftEvenOdd; 
        else
            return m_SubjFillType == PolyFillType.pftEvenOdd;
      }
      //------------------------------------------------------------------------------

      private bool IsContributing(TEdge edge)
      {
          PolyFillType pft, pft2;
          if (edge.PolyTyp == PolyType.ptSubject)
          {
              pft = m_SubjFillType;
              pft2 = m_ClipFillType;
          }
          else
          {
              pft = m_ClipFillType;
              pft2 = m_SubjFillType;
          }

          switch (pft)
          {
              case PolyFillType.pftEvenOdd:
                  //return false if a subj line has been flagged as inside a subj polygon
                  if (edge.WindDelta == 0 && edge.WindCnt != 1) return false;
                  break;
              case PolyFillType.pftNonZero:
                  if (Math.Abs(edge.WindCnt) != 1) return false;
                  break;
              case PolyFillType.pftPositive:
                  if (edge.WindCnt != 1) return false;
                  break;
              default: //PolyFillType.pftNegative
                  if (edge.WindCnt != -1) return false; 
                  break;
          }

          switch (m_ClipType)
          {
            case ClipType.ctIntersection:
                switch (pft2)
                {
                    case PolyFillType.pftEvenOdd:
                    case PolyFillType.pftNonZero:
                        return (edge.WindCnt2 != 0);
                    case PolyFillType.pftPositive:
                        return (edge.WindCnt2 > 0);
                    default:
                        return (edge.WindCnt2 < 0);
                }
            case ClipType.ctUnion:
                switch (pft2)
                {
                    case PolyFillType.pftEvenOdd:
                    case PolyFillType.pftNonZero:
                        return (edge.WindCnt2 == 0);
                    case PolyFillType.pftPositive:
                        return (edge.WindCnt2 <= 0);
                    default:
                        return (edge.WindCnt2 >= 0);
                }
            case ClipType.ctDifference:
                if (edge.PolyTyp == PolyType.ptSubject)
                    switch (pft2)
                    {
                        case PolyFillType.pftEvenOdd:
                        case PolyFillType.pftNonZero:
                            return (edge.WindCnt2 == 0);
                        case PolyFillType.pftPositive:
                            return (edge.WindCnt2 <= 0);
                        default:
                            return (edge.WindCnt2 >= 0);
                    }
                else
                    switch (pft2)
                    {
                        case PolyFillType.pftEvenOdd:
                        case PolyFillType.pftNonZero:
                            return (edge.WindCnt2 != 0);
                        case PolyFillType.pftPositive:
                            return (edge.WindCnt2 > 0);
                        default:
                            return (edge.WindCnt2 < 0);
                    }
            case ClipType.ctXor:
                if (edge.WindDelta == 0) //XOr always contributing unless open
                  switch (pft2)
                  {
                    case PolyFillType.pftEvenOdd:
                    case PolyFillType.pftNonZero:
                      return (edge.WindCnt2 == 0);
                    case PolyFillType.pftPositive:
                      return (edge.WindCnt2 <= 0);
                    default:
                      return (edge.WindCnt2 >= 0);
                  }
                else
                  return true;
          }
          return true;
      }
      //------------------------------------------------------------------------------

      private void SetWindingCount(TEdge edge)
      {
        TEdge e = edge.PrevInAEL;
        //find the edge of the same polytype that immediately preceeds 'edge' in AEL
        while (e != null && ((e.PolyTyp != edge.PolyTyp) || (e.WindDelta == 0))) e = e.PrevInAEL;
        if (e == null)
        {
          edge.WindCnt = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
          edge.WindCnt2 = 0;
          e = m_ActiveEdges; //ie get ready to calc WindCnt2
        }
        else if (edge.WindDelta == 0 && m_ClipType != ClipType.ctUnion)
        {
          edge.WindCnt = 1;
          edge.WindCnt2 = e.WindCnt2;
          e = e.NextInAEL; //ie get ready to calc WindCnt2
        }
        else if (IsEvenOddFillType(edge))
        {
          //EvenOdd filling ...
          if (edge.WindDelta == 0)
          {
            //are we inside a subj polygon ...
            bool Inside = true;
            TEdge e2 = e.PrevInAEL;
            while (e2 != null)
            {
              if (e2.PolyTyp == e.PolyTyp && e2.WindDelta != 0)
                Inside = !Inside;
              e2 = e2.PrevInAEL;
            }
            edge.WindCnt = (Inside ? 0 : 1);
          }
          else
          {
            edge.WindCnt = edge.WindDelta;
          }
          edge.WindCnt2 = e.WindCnt2;
          e = e.NextInAEL; //ie get ready to calc WindCnt2
        }
        else
        {
          //nonZero, Positive or Negative filling ...
          if (e.WindCnt * e.WindDelta < 0)
          {
            //prev edge is 'decreasing' WindCount (WC) toward zero
            //so we're outside the previous polygon ...
            if (Math.Abs(e.WindCnt) > 1)
            {
              //outside prev poly but still inside another.
              //when reversing direction of prev poly use the same WC 
              if (e.WindDelta * edge.WindDelta < 0) edge.WindCnt = e.WindCnt;
              //otherwise continue to 'decrease' WC ...
              else edge.WindCnt = e.WindCnt + edge.WindDelta;
            }
            else
              //now outside all polys of same polytype so set own WC ...
              edge.WindCnt = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
          }
          else
          {
            //prev edge is 'increasing' WindCount (WC) away from zero
            //so we're inside the previous polygon ...
            if (edge.WindDelta == 0)
              edge.WindCnt = (e.WindCnt < 0 ? e.WindCnt - 1 : e.WindCnt + 1);
            //if wind direction is reversing prev then use same WC
            else if (e.WindDelta * edge.WindDelta < 0)
              edge.WindCnt = e.WindCnt;
            //otherwise add to WC ...
            else edge.WindCnt = e.WindCnt + edge.WindDelta;
          }
          edge.WindCnt2 = e.WindCnt2;
          e = e.NextInAEL; //ie get ready to calc WindCnt2
        }

        //update WindCnt2 ...
        if (IsEvenOddAltFillType(edge))
        {
          //EvenOdd filling ...
          while (e != edge)
          {
            if (e.WindDelta != 0)
              edge.WindCnt2 = (edge.WindCnt2 == 0 ? 1 : 0);
            e = e.NextInAEL;
          }
        }
        else
        {
          //nonZero, Positive or Negative filling ...
          while (e != edge)
          {
            edge.WindCnt2 += e.WindDelta;
            e = e.NextInAEL;
          }
        }
      }
      //------------------------------------------------------------------------------

      private void AddEdgeToSEL(TEdge edge)
      {
          //SEL pointers in PEdge are reused to build a list of horizontal edges.
          //However, we don't need to worry about order with horizontal edge processing.
          if (m_SortedEdges == null)
          {
              m_SortedEdges = edge;
              edge.PrevInSEL = null;
              edge.NextInSEL = null;
          }
          else
          {
              edge.NextInSEL = m_SortedEdges;
              edge.PrevInSEL = null;
              m_SortedEdges.PrevInSEL = edge;
              m_SortedEdges = edge;
          }
      }
      //------------------------------------------------------------------------------

      private void CopyAELToSEL()
      {
          TEdge e = m_ActiveEdges;
          m_SortedEdges = e;
          while (e != null)
          {
              e.PrevInSEL = e.PrevInAEL;
              e.NextInSEL = e.NextInAEL;
              e = e.NextInAEL;
          }
      }
      //------------------------------------------------------------------------------

      private void SwapPositionsInAEL(TEdge edge1, TEdge edge2)
      {
        //check that one or other edge hasn't already been removed from AEL ...
          if (edge1.NextInAEL == edge1.PrevInAEL ||
            edge2.NextInAEL == edge2.PrevInAEL) return;
        
          if (edge1.NextInAEL == edge2)
          {
              TEdge next = edge2.NextInAEL;
              if (next != null)
                  next.PrevInAEL = edge1;
              TEdge prev = edge1.PrevInAEL;
              if (prev != null)
                  prev.NextInAEL = edge2;
              edge2.PrevInAEL = prev;
              edge2.NextInAEL = edge1;
              edge1.PrevInAEL = edge2;
              edge1.NextInAEL = next;
          }
          else if (edge2.NextInAEL == edge1)
          {
              TEdge next = edge1.NextInAEL;
              if (next != null)
                  next.PrevInAEL = edge2;
              TEdge prev = edge2.PrevInAEL;
              if (prev != null)
                  prev.NextInAEL = edge1;
              edge1.PrevInAEL = prev;
              edge1.NextInAEL = edge2;
              edge2.PrevInAEL = edge1;
              edge2.NextInAEL = next;
          }
          else
          {
              TEdge next = edge1.NextInAEL;
              TEdge prev = edge1.PrevInAEL;
              edge1.NextInAEL = edge2.NextInAEL;
              if (edge1.NextInAEL != null)
                  edge1.NextInAEL.PrevInAEL = edge1;
              edge1.PrevInAEL = edge2.PrevInAEL;
              if (edge1.PrevInAEL != null)
                  edge1.PrevInAEL.NextInAEL = edge1;
              edge2.NextInAEL = next;
              if (edge2.NextInAEL != null)
                  edge2.NextInAEL.PrevInAEL = edge2;
              edge2.PrevInAEL = prev;
              if (edge2.PrevInAEL != null)
                  edge2.PrevInAEL.NextInAEL = edge2;
          }

          if (edge1.PrevInAEL == null)
              m_ActiveEdges = edge1;
          else if (edge2.PrevInAEL == null)
              m_ActiveEdges = edge2;
      }
      //------------------------------------------------------------------------------

      private void SwapPositionsInSEL(TEdge edge1, TEdge edge2)
      {
          if (edge1.NextInSEL == null && edge1.PrevInSEL == null)
              return;
          if (edge2.NextInSEL == null && edge2.PrevInSEL == null)
              return;

          if (edge1.NextInSEL == edge2)
          {
              TEdge next = edge2.NextInSEL;
              if (next != null)
                  next.PrevInSEL = edge1;
              TEdge prev = edge1.PrevInSEL;
              if (prev != null)
                  prev.NextInSEL = edge2;
              edge2.PrevInSEL = prev;
              edge2.NextInSEL = edge1;
              edge1.PrevInSEL = edge2;
              edge1.NextInSEL = next;
          }
          else if (edge2.NextInSEL == edge1)
          {
              TEdge next = edge1.NextInSEL;
              if (next != null)
                  next.PrevInSEL = edge2;
              TEdge prev = edge2.PrevInSEL;
              if (prev != null)
                  prev.NextInSEL = edge1;
              edge1.PrevInSEL = prev;
              edge1.NextInSEL = edge2;
              edge2.PrevInSEL = edge1;
              edge2.NextInSEL = next;
          }
          else
          {
              TEdge next = edge1.NextInSEL;
              TEdge prev = edge1.PrevInSEL;
              edge1.NextInSEL = edge2.NextInSEL;
              if (edge1.NextInSEL != null)
                  edge1.NextInSEL.PrevInSEL = edge1;
              edge1.PrevInSEL = edge2.PrevInSEL;
              if (edge1.PrevInSEL != null)
                  edge1.PrevInSEL.NextInSEL = edge1;
              edge2.NextInSEL = next;
              if (edge2.NextInSEL != null)
                  edge2.NextInSEL.PrevInSEL = edge2;
              edge2.PrevInSEL = prev;
              if (edge2.PrevInSEL != null)
                  edge2.PrevInSEL.NextInSEL = edge2;
          }

          if (edge1.PrevInSEL == null)
              m_SortedEdges = edge1;
          else if (edge2.PrevInSEL == null)
              m_SortedEdges = edge2;
      }
      //------------------------------------------------------------------------------


      private void AddLocalMaxPoly(TEdge e1, TEdge e2, IntPoint pt)
      {
          AddOutPt(e1, pt);
          if (e1.OutIdx == e2.OutIdx)
          {
              e1.OutIdx = Unassigned;
              e2.OutIdx = Unassigned;
          }
          else if (e1.OutIdx < e2.OutIdx) 
              AppendPolygon(e1, e2);
          else 
              AppendPolygon(e2, e1);
      }
      //------------------------------------------------------------------------------

      private OutPt AddLocalMinPoly(TEdge e1, TEdge e2, IntPoint pt)
      {
        OutPt result;
        TEdge e, prevE;
        if (IsHorizontal(e2) || (e1.Dx > e2.Dx))
        {
          result = AddOutPt(e1, pt);
          e2.OutIdx = e1.OutIdx;
          e1.Side = EdgeSide.esLeft;
          e2.Side = EdgeSide.esRight;
          e = e1;
          if (e.PrevInAEL == e2)
            prevE = e2.PrevInAEL; 
          else
            prevE = e.PrevInAEL;
        }
        else
        {
          result = AddOutPt(e2, pt);
          e1.OutIdx = e2.OutIdx;
          e1.Side = EdgeSide.esRight;
          e2.Side = EdgeSide.esLeft;
          e = e2;
          if (e.PrevInAEL == e1)
              prevE = e1.PrevInAEL;
          else
              prevE = e.PrevInAEL;
        }

        if (prevE != null && prevE.OutIdx >= 0 &&
            (TopX(prevE, pt.Y) == TopX(e, pt.Y)) &&
            SlopesEqual(e, prevE, m_UseFullRange) &&
            (e.WindDelta != 0) && (prevE.WindDelta != 0))
        {
          OutPt outPt = AddOutPt(prevE, pt);
          AddJoin(result, outPt, e.Top);
        }
        return result;
      }
      //------------------------------------------------------------------------------

      private OutRec CreateOutRec()
      {
        OutRec result = new OutRec();
        result.Idx = Unassigned;
        result.IsHole = false;
        result.IsOpen = false;
        result.FirstLeft = null;
        result.Pts = null;
        result.BottomPt = null;
        result.PolyNode = null;
        m_PolyOuts.Add(result);
        result.Idx = m_PolyOuts.Count - 1;
        return result;
      }
      //------------------------------------------------------------------------------

      private OutPt AddOutPt(TEdge e, IntPoint pt)
      {
        bool ToFront = (e.Side == EdgeSide.esLeft);
        if(  e.OutIdx < 0 )
        {
          OutRec outRec = CreateOutRec();
          outRec.IsOpen = (e.WindDelta == 0);
          e.OutIdx = outRec.Idx;
          OutPt newOp = new OutPt();
          outRec.Pts = newOp;
          newOp.Pt = pt;
          newOp.Idx = outRec.Idx;
          newOp.Next = newOp;
          newOp.Prev = newOp;
          if (!outRec.IsOpen)
            SetHoleState(e, outRec);
          return newOp;
        } else
        {
          OutRec outRec = m_PolyOuts[e.OutIdx];
          OutPt op = outRec.Pts;
          if (ToFront && pt == op.Pt) return op;
          else if (!ToFront && pt == op.Prev.Pt) return op.Prev;

          OutPt newOp = new OutPt();
          newOp.Pt = pt;
          newOp.Idx = outRec.Idx;
          newOp.Next = op;
          newOp.Prev = op.Prev;
          newOp.Prev.Next = newOp;
          op.Prev = newOp;
          if (ToFront) outRec.Pts = newOp;
          return newOp;
        }
      }
      //------------------------------------------------------------------------------

      internal void SwapPoints(ref IntPoint pt1, ref IntPoint pt2)
      {
          IntPoint tmp = new IntPoint(pt1);
          pt1 = pt2;
          pt2 = tmp;
      }
      //------------------------------------------------------------------------------

      private bool HorzSegmentsOverlap(
        IntPoint Pt1a, IntPoint Pt1b, IntPoint Pt2a, IntPoint Pt2b)
      {
        //precondition: both segments are horizontal
        if ((Pt1a.X > Pt2a.X) == (Pt1a.X < Pt2b.X)) return true;
        else if ((Pt1b.X > Pt2a.X) == (Pt1b.X < Pt2b.X)) return true;
        else if ((Pt2a.X > Pt1a.X) == (Pt2a.X < Pt1b.X)) return true;
        else if ((Pt2b.X > Pt1a.X) == (Pt2b.X < Pt1b.X)) return true;
        else if ((Pt1a.X == Pt2a.X) && (Pt1b.X == Pt2b.X)) return true;
        else if ((Pt1a.X == Pt2b.X) && (Pt1b.X == Pt2a.X)) return true;
        else return false;
      }
      //------------------------------------------------------------------------------
  
      private OutPt InsertPolyPtBetween(OutPt p1, OutPt p2, IntPoint pt)
      {
          OutPt result = new OutPt();
          result.Pt = pt;
          if (p2 == p1.Next)
          {
              p1.Next = result;
              p2.Prev = result;
              result.Next = p2;
              result.Prev = p1;
          } else
          {
              p2.Next = result;
              p1.Prev = result;
              result.Next = p1;
              result.Prev = p2;
          }
          return result;
      }
      //------------------------------------------------------------------------------

      private void SetHoleState(TEdge e, OutRec outRec)
      {
          bool isHole = false;
          TEdge e2 = e.PrevInAEL;
          while (e2 != null)
          {
              if (e2.OutIdx >= 0)
              {
                  isHole = !isHole;
                  if (outRec.FirstLeft == null)
                      outRec.FirstLeft = m_PolyOuts[e2.OutIdx];
              }
              e2 = e2.PrevInAEL;
          }
          if (isHole) outRec.IsHole = true;
      }
      //------------------------------------------------------------------------------

      private double GetDx(IntPoint pt1, IntPoint pt2)
      {
          if (pt1.Y == pt2.Y) return horizontal;
          else return (double)(pt2.X - pt1.X) / (pt2.Y - pt1.Y);
      }
      //---------------------------------------------------------------------------

      private bool FirstIsBottomPt(OutPt btmPt1, OutPt btmPt2)
      {
        OutPt p = btmPt1.Prev;
        while ((p.Pt == btmPt1.Pt) && (p != btmPt1)) p = p.Prev;
        double dx1p = Math.Abs(GetDx(btmPt1.Pt, p.Pt));
        p = btmPt1.Next;
        while ((p.Pt == btmPt1.Pt) && (p != btmPt1)) p = p.Next;
        double dx1n = Math.Abs(GetDx(btmPt1.Pt, p.Pt));

        p = btmPt2.Prev;
        while ((p.Pt == btmPt2.Pt) && (p != btmPt2)) p = p.Prev;
        double dx2p = Math.Abs(GetDx(btmPt2.Pt, p.Pt));
        p = btmPt2.Next;
        while ((p.Pt == btmPt2.Pt) && (p != btmPt2)) p = p.Next;
        double dx2n = Math.Abs(GetDx(btmPt2.Pt, p.Pt));
        return (dx1p >= dx2p && dx1p >= dx2n) || (dx1n >= dx2p && dx1n >= dx2n);
      }
      //------------------------------------------------------------------------------

      private OutPt GetBottomPt(OutPt pp)
      {
        OutPt dups = null;
        OutPt p = pp.Next;
        while (p != pp)
        {
          if (p.Pt.Y > pp.Pt.Y)
          {
            pp = p;
            dups = null;
          }
          else if (p.Pt.Y == pp.Pt.Y && p.Pt.X <= pp.Pt.X)
          {
            if (p.Pt.X < pp.Pt.X)
            {
                dups = null;
                pp = p;
            } else
            {
              if (p.Next != pp && p.Prev != pp) dups = p;
            }
          }
          p = p.Next;
        }
        if (dups != null)
        {
          //there appears to be at least 2 vertices at bottomPt so ...
          while (dups != p)
          {
            if (!FirstIsBottomPt(p, dups)) pp = dups;
            dups = dups.Next;
            while (dups.Pt != pp.Pt) dups = dups.Next;
          }
        }
        return pp;
      }
      //------------------------------------------------------------------------------

      private OutRec GetLowermostRec(OutRec outRec1, OutRec outRec2)
      {
          //work out which polygon fragment has the correct hole state ...
          if (outRec1.BottomPt == null) 
              outRec1.BottomPt = GetBottomPt(outRec1.Pts);
          if (outRec2.BottomPt == null) 
              outRec2.BottomPt = GetBottomPt(outRec2.Pts);
          OutPt bPt1 = outRec1.BottomPt;
          OutPt bPt2 = outRec2.BottomPt;
          if (bPt1.Pt.Y > bPt2.Pt.Y) return outRec1;
          else if (bPt1.Pt.Y < bPt2.Pt.Y) return outRec2;
          else if (bPt1.Pt.X < bPt2.Pt.X) return outRec1;
          else if (bPt1.Pt.X > bPt2.Pt.X) return outRec2;
          else if (bPt1.Next == bPt1) return outRec2;
          else if (bPt2.Next == bPt2) return outRec1;
          else if (FirstIsBottomPt(bPt1, bPt2)) return outRec1;
          else return outRec2;
      }
      //------------------------------------------------------------------------------

      bool Param1RightOfParam2(OutRec outRec1, OutRec outRec2)
      {
          do
          {
              outRec1 = outRec1.FirstLeft;
              if (outRec1 == outRec2) return true;
          } while (outRec1 != null);
          return false;
      }
      //------------------------------------------------------------------------------

      private OutRec GetOutRec(int idx)
      {
        OutRec outrec = m_PolyOuts[idx];
        while (outrec != m_PolyOuts[outrec.Idx])
          outrec = m_PolyOuts[outrec.Idx];
        return outrec;
      }
      //------------------------------------------------------------------------------

      private void AppendPolygon(TEdge e1, TEdge e2)
      {
        //get the start and ends of both output polygons ...
        OutRec outRec1 = m_PolyOuts[e1.OutIdx];
        OutRec outRec2 = m_PolyOuts[e2.OutIdx];

        OutRec holeStateRec;
        if (Param1RightOfParam2(outRec1, outRec2)) 
            holeStateRec = outRec2;
        else if (Param1RightOfParam2(outRec2, outRec1))
            holeStateRec = outRec1;
        else
            holeStateRec = GetLowermostRec(outRec1, outRec2);

        OutPt p1_lft = outRec1.Pts;
        OutPt p1_rt = p1_lft.Prev;
        OutPt p2_lft = outRec2.Pts;
        OutPt p2_rt = p2_lft.Prev;

        EdgeSide side;
        //join e2 poly onto e1 poly and delete pointers to e2 ...
        if(  e1.Side == EdgeSide.esLeft )
        {
          if (e2.Side == EdgeSide.esLeft)
          {
            //z y x a b c
            ReversePolyPtLinks(p2_lft);
            p2_lft.Next = p1_lft;
            p1_lft.Prev = p2_lft;
            p1_rt.Next = p2_rt;
            p2_rt.Prev = p1_rt;
            outRec1.Pts = p2_rt;
          } else
          {
            //x y z a b c
            p2_rt.Next = p1_lft;
            p1_lft.Prev = p2_rt;
            p2_lft.Prev = p1_rt;
            p1_rt.Next = p2_lft;
            outRec1.Pts = p2_lft;
          }
          side = EdgeSide.esLeft;
        } else
        {
          if (e2.Side == EdgeSide.esRight)
          {
            //a b c z y x
            ReversePolyPtLinks( p2_lft );
            p1_rt.Next = p2_rt;
            p2_rt.Prev = p1_rt;
            p2_lft.Next = p1_lft;
            p1_lft.Prev = p2_lft;
          } else
          {
            //a b c x y z
            p1_rt.Next = p2_lft;
            p2_lft.Prev = p1_rt;
            p1_lft.Prev = p2_rt;
            p2_rt.Next = p1_lft;
          }
          side = EdgeSide.esRight;
        }

        outRec1.BottomPt = null; 
        if (holeStateRec == outRec2)
        {
            if (outRec2.FirstLeft != outRec1)
                outRec1.FirstLeft = outRec2.FirstLeft;
            outRec1.IsHole = outRec2.IsHole;
        }
        outRec2.Pts = null;
        outRec2.BottomPt = null;

        outRec2.FirstLeft = outRec1;

        int OKIdx = e1.OutIdx;
        int ObsoleteIdx = e2.OutIdx;

        e1.OutIdx = Unassigned; //nb: safe because we only get here via AddLocalMaxPoly
        e2.OutIdx = Unassigned;

        TEdge e = m_ActiveEdges;
        while( e != null )
        {
          if( e.OutIdx == ObsoleteIdx )
          {
            e.OutIdx = OKIdx;
            e.Side = side;
            break;
          }
          e = e.NextInAEL;
        }
        outRec2.Idx = outRec1.Idx;
      }
      //------------------------------------------------------------------------------

      private void ReversePolyPtLinks(OutPt pp)
      {
          if (pp == null) return;
          OutPt pp1;
          OutPt pp2;
          pp1 = pp;
          do
          {
              pp2 = pp1.Next;
              pp1.Next = pp1.Prev;
              pp1.Prev = pp2;
              pp1 = pp2;
          } while (pp1 != pp);
      }
      //------------------------------------------------------------------------------

      private static void SwapSides(TEdge edge1, TEdge edge2)
      {
          EdgeSide side = edge1.Side;
          edge1.Side = edge2.Side;
          edge2.Side = side;
      }
      //------------------------------------------------------------------------------

      private static void SwapPolyIndexes(TEdge edge1, TEdge edge2)
      {
          int outIdx = edge1.OutIdx;
          edge1.OutIdx = edge2.OutIdx;
          edge2.OutIdx = outIdx;
      }
      //------------------------------------------------------------------------------

      private void IntersectEdges(TEdge e1, TEdge e2, IntPoint pt, bool protect = false)
      {
          //e1 will be to the left of e2 BELOW the intersection. Therefore e1 is before
          //e2 in AEL except when e1 is being inserted at the intersection point ...

          bool e1stops = !protect && e1.NextInLML == null &&
            e1.Top.X == pt.X && e1.Top.Y == pt.Y;
          bool e2stops = !protect && e2.NextInLML == null &&
            e2.Top.X == pt.X && e2.Top.Y == pt.Y;
          bool e1Contributing = (e1.OutIdx >= 0);
          bool e2Contributing = (e2.OutIdx >= 0);

#if use_lines
          //if either edge is on an OPEN path ...
          if (e1.WindDelta == 0 || e2.WindDelta == 0)
          {
            //ignore subject-subject open path intersections UNLESS they
            //are both open paths, AND they are both 'contributing maximas' ...
            if (e1.WindDelta == 0 && e2.WindDelta == 0 && (e1stops || e2stops))
            {
              if (e1Contributing && e2Contributing)
                AddLocalMaxPoly(e1, e2, pt);
            }
            //if intersecting a subj line with a subj poly ...
            else if (e1.PolyTyp == e2.PolyTyp && 
              e1.WindDelta != e2.WindDelta && m_ClipType == ClipType.ctUnion)
            {
              if (e1.WindDelta == 0)
              {
                if (e2Contributing)
                {
                  AddOutPt(e1, pt);
                  if (e1Contributing) e1.OutIdx = Unassigned;
                }
              }
              else
              {
                if (e1Contributing)
                {
                  AddOutPt(e2, pt);
                  if (e2Contributing) e2.OutIdx = Unassigned;
                }
              }
            }
            else if (e1.PolyTyp != e2.PolyTyp)
            {
              //toggle subj open path OutIdx on/off when Abs(clip.WndCnt) == 1 ...
              if ((e1.WindDelta == 0) && Math.Abs(e2.WindCnt) == 1 && 
                (m_ClipType != ClipType.ctUnion || e2.WindCnt2 == 0))
              {
                AddOutPt(e1, pt);
                if (e1Contributing) e1.OutIdx = Unassigned;
              }
              else if ((e2.WindDelta == 0) && (Math.Abs(e1.WindCnt) == 1) && 
                (m_ClipType != ClipType.ctUnion || e1.WindCnt2 == 0))
              {
                AddOutPt(e2, pt);
                if (e2Contributing) e2.OutIdx = Unassigned;
              }
            }

            if (e1stops)
              if (e1.OutIdx < 0) DeleteFromAEL(e1);
              else throw new ClipperException("Error intersecting polylines");
            if (e2stops) 
              if (e2.OutIdx < 0) DeleteFromAEL(e2);
              else throw new ClipperException("Error intersecting polylines");
            return;
          }
#endif

  //update winding counts...
  //assumes that e1 will be to the Right of e2 ABOVE the intersection
          if (e1.PolyTyp == e2.PolyTyp)
          {
              if (IsEvenOddFillType(e1))
              {
                  int oldE1WindCnt = e1.WindCnt;
                  e1.WindCnt = e2.WindCnt;
                  e2.WindCnt = oldE1WindCnt;
              }
              else
              {
                  if (e1.WindCnt + e2.WindDelta == 0) e1.WindCnt = -e1.WindCnt;
                  else e1.WindCnt += e2.WindDelta;
                  if (e2.WindCnt - e1.WindDelta == 0) e2.WindCnt = -e2.WindCnt;
                  else e2.WindCnt -= e1.WindDelta;
              }
          }
          else
          {
              if (!IsEvenOddFillType(e2)) e1.WindCnt2 += e2.WindDelta;
              else e1.WindCnt2 = (e1.WindCnt2 == 0) ? 1 : 0;
              if (!IsEvenOddFillType(e1)) e2.WindCnt2 -= e1.WindDelta;
              else e2.WindCnt2 = (e2.WindCnt2 == 0) ? 1 : 0;
          }

          PolyFillType e1FillType, e2FillType, e1FillType2, e2FillType2;
          if (e1.PolyTyp == PolyType.ptSubject)
          {
              e1FillType = m_SubjFillType;
              e1FillType2 = m_ClipFillType;
          }
          else
          {
              e1FillType = m_ClipFillType;
              e1FillType2 = m_SubjFillType;
          }
          if (e2.PolyTyp == PolyType.ptSubject)
          {
              e2FillType = m_SubjFillType;
              e2FillType2 = m_ClipFillType;
          }
          else
          {
              e2FillType = m_ClipFillType;
              e2FillType2 = m_SubjFillType;
          }

          int e1Wc, e2Wc;
          switch (e1FillType)
          {
              case PolyFillType.pftPositive: e1Wc = e1.WindCnt; break;
              case PolyFillType.pftNegative: e1Wc = -e1.WindCnt; break;
              default: e1Wc = Math.Abs(e1.WindCnt); break;
          }
          switch (e2FillType)
          {
              case PolyFillType.pftPositive: e2Wc = e2.WindCnt; break;
              case PolyFillType.pftNegative: e2Wc = -e2.WindCnt; break;
              default: e2Wc = Math.Abs(e2.WindCnt); break;
          }

          if (e1Contributing && e2Contributing)
          {
              if ( e1stops || e2stops || 
                (e1Wc != 0 && e1Wc != 1) || (e2Wc != 0 && e2Wc != 1) ||
                (e1.PolyTyp != e2.PolyTyp && m_ClipType != ClipType.ctXor))
                  AddLocalMaxPoly(e1, e2, pt);
              else
              {
                  AddOutPt(e1, pt);
                  AddOutPt(e2, pt);
                  SwapSides(e1, e2);
                  SwapPolyIndexes(e1, e2);
              }
          }
          else if (e1Contributing)
          {
              if (e2Wc == 0 || e2Wc == 1)
              {
                  AddOutPt(e1, pt);
                  SwapSides(e1, e2);
                  SwapPolyIndexes(e1, e2);
              }

          }
          else if (e2Contributing)
          {
              if (e1Wc == 0 || e1Wc == 1)
              {
                  AddOutPt(e2, pt);
                  SwapSides(e1, e2);
                  SwapPolyIndexes(e1, e2);
              }
          }
          else if ( (e1Wc == 0 || e1Wc == 1) && 
              (e2Wc == 0 || e2Wc == 1) && !e1stops && !e2stops )
          {
              //neither edge is currently contributing ...
              cInt e1Wc2, e2Wc2;
              switch (e1FillType2)
              {
                  case PolyFillType.pftPositive: e1Wc2 = e1.WindCnt2; break;
                  case PolyFillType.pftNegative: e1Wc2 = -e1.WindCnt2; break;
                  default: e1Wc2 = Math.Abs(e1.WindCnt2); break;
              }
              switch (e2FillType2)
              {
                  case PolyFillType.pftPositive: e2Wc2 = e2.WindCnt2; break;
                  case PolyFillType.pftNegative: e2Wc2 = -e2.WindCnt2; break;
                  default: e2Wc2 = Math.Abs(e2.WindCnt2); break;
              }

              if (e1.PolyTyp != e2.PolyTyp)
                  AddLocalMinPoly(e1, e2, pt);
              else if (e1Wc == 1 && e2Wc == 1)
                  switch (m_ClipType)
                  {
                      case ClipType.ctIntersection:
                          if (e1Wc2 > 0 && e2Wc2 > 0)
                              AddLocalMinPoly(e1, e2, pt);
                          break;
                      case ClipType.ctUnion:
                          if (e1Wc2 <= 0 && e2Wc2 <= 0)
                              AddLocalMinPoly(e1, e2, pt);
                          break;
                      case ClipType.ctDifference:
                          if (((e1.PolyTyp == PolyType.ptClip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                              ((e1.PolyTyp == PolyType.ptSubject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                                  AddLocalMinPoly(e1, e2, pt);
                          break;
                      case ClipType.ctXor:
                          AddLocalMinPoly(e1, e2, pt);
                          break;
                  }
              else 
                  SwapSides(e1, e2);
          }

          if ((e1stops != e2stops) &&
            ((e1stops && (e1.OutIdx >= 0)) || (e2stops && (e2.OutIdx >= 0))))
          {
              SwapSides(e1, e2);
              SwapPolyIndexes(e1, e2);
          }

          //finally, delete any non-contributing maxima edges  ...
          if (e1stops) DeleteFromAEL(e1);
          if (e2stops) DeleteFromAEL(e2);
      }
      //------------------------------------------------------------------------------

      private void DeleteFromAEL(TEdge e)
      {
          TEdge AelPrev = e.PrevInAEL;
          TEdge AelNext = e.NextInAEL;
          if (AelPrev == null && AelNext == null && (e != m_ActiveEdges))
              return; //already deleted
          if (AelPrev != null)
              AelPrev.NextInAEL = AelNext;
          else m_ActiveEdges = AelNext;
          if (AelNext != null)
              AelNext.PrevInAEL = AelPrev;
          e.NextInAEL = null;
          e.PrevInAEL = null;
      }
      //------------------------------------------------------------------------------

      private void DeleteFromSEL(TEdge e)
      {
          TEdge SelPrev = e.PrevInSEL;
          TEdge SelNext = e.NextInSEL;
          if (SelPrev == null && SelNext == null && (e != m_SortedEdges))
              return; //already deleted
          if (SelPrev != null)
              SelPrev.NextInSEL = SelNext;
          else m_SortedEdges = SelNext;
          if (SelNext != null)
              SelNext.PrevInSEL = SelPrev;
          e.NextInSEL = null;
          e.PrevInSEL = null;
      }
      //------------------------------------------------------------------------------

      private void UpdateEdgeIntoAEL(ref TEdge e)
      {
          if (e.NextInLML == null)
              throw new ClipperException("UpdateEdgeIntoAEL: invalid call");
          TEdge AelPrev = e.PrevInAEL;
          TEdge AelNext = e.NextInAEL;
          e.NextInLML.OutIdx = e.OutIdx;
          if (AelPrev != null)
              AelPrev.NextInAEL = e.NextInLML;
          else m_ActiveEdges = e.NextInLML;
          if (AelNext != null)
              AelNext.PrevInAEL = e.NextInLML;
          e.NextInLML.Side = e.Side;
          e.NextInLML.WindDelta = e.WindDelta;
          e.NextInLML.WindCnt = e.WindCnt;
          e.NextInLML.WindCnt2 = e.WindCnt2;
          e = e.NextInLML;
          e.Curr = e.Bot;
          e.PrevInAEL = AelPrev;
          e.NextInAEL = AelNext;
          if (!IsHorizontal(e)) InsertScanbeam(e.Top.Y);
      }
      //------------------------------------------------------------------------------

      private void ProcessHorizontals(bool isTopOfScanbeam)
      {
          TEdge horzEdge = m_SortedEdges;
          while (horzEdge != null)
          {
              DeleteFromSEL(horzEdge);
              ProcessHorizontal(horzEdge, isTopOfScanbeam);
              horzEdge = m_SortedEdges;
          }
      }
      //------------------------------------------------------------------------------

      void GetHorzDirection(TEdge HorzEdge, out Direction Dir, out cInt Left, out cInt Right)
      {
        if (HorzEdge.Bot.X < HorzEdge.Top.X)
        {
          Left = HorzEdge.Bot.X;
          Right = HorzEdge.Top.X;
          Dir = Direction.dLeftToRight;
        } else
        {
          Left = HorzEdge.Top.X;
          Right = HorzEdge.Bot.X;
          Dir = Direction.dRightToLeft;
        }
      }
      //------------------------------------------------------------------------

      void PrepareHorzJoins(TEdge horzEdge, bool isTopOfScanbeam)
      {
        //get the last Op for this horizontal edge
        //the point may be anywhere along the horizontal ...
        OutPt outPt = m_PolyOuts[horzEdge.OutIdx].Pts;
        if (horzEdge.Side != EdgeSide.esLeft) outPt = outPt.Prev;

        //First, match up overlapping horizontal edges (eg when one polygon's
        //intermediate horz edge overlaps an intermediate horz edge of another, or
        //when one polygon sits on top of another) ...
        for (int i = 0; i < m_GhostJoins.Count; ++i)
        {
          Join j = m_GhostJoins[i];
          if (HorzSegmentsOverlap(j.OutPt1.Pt, j.OffPt, horzEdge.Bot, horzEdge.Top))
              AddJoin(j.OutPt1, outPt, j.OffPt);
        }
        //Also, since horizontal edges at the top of one SB are often removed from
        //the AEL before we process the horizontal edges at the bottom of the next,
        //we need to create 'ghost' Join records of 'contrubuting' horizontals that
        //we can compare with horizontals at the bottom of the next SB.
        if (isTopOfScanbeam) 
          if (outPt.Pt == horzEdge.Top)
            AddGhostJoin(outPt, horzEdge.Bot); 
          else
            AddGhostJoin(outPt, horzEdge.Top);
      }
      //------------------------------------------------------------------------------

      private void ProcessHorizontal(TEdge horzEdge, bool isTopOfScanbeam)
      {
        Direction dir;
        cInt horzLeft, horzRight;

        GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

        TEdge eLastHorz = horzEdge, eMaxPair = null;
        while (eLastHorz.NextInLML != null && IsHorizontal(eLastHorz.NextInLML)) 
          eLastHorz = eLastHorz.NextInLML;
        if (eLastHorz.NextInLML == null)
          eMaxPair = GetMaximaPair(eLastHorz);

        for (;;)
        {
          bool IsLastHorz = (horzEdge == eLastHorz);
          TEdge e = GetNextInAEL(horzEdge, dir);
          while(e != null)
          {
            //Break if we've got to the end of an intermediate horizontal edge ...
            //nb: Smaller Dx's are to the right of larger Dx's ABOVE the horizontal.
            if (e.Curr.X == horzEdge.Top.X && horzEdge.NextInLML != null && 
              e.Dx < horzEdge.NextInLML.Dx) break;

            TEdge eNext = GetNextInAEL(e, dir); //saves eNext for later

            if ((dir == Direction.dLeftToRight && e.Curr.X <= horzRight) ||
              (dir == Direction.dRightToLeft && e.Curr.X >= horzLeft))
            {
              //so far we're still in range of the horizontal Edge  but make sure
              //we're at the last of consec. horizontals when matching with eMaxPair
              if(e == eMaxPair && IsLastHorz)
              {
                if (horzEdge.OutIdx >= 0 && horzEdge.WindDelta != 0) 
                  PrepareHorzJoins(horzEdge, isTopOfScanbeam);
                if (dir == Direction.dLeftToRight)
                  IntersectEdges(horzEdge, e, e.Top);
                else
                  IntersectEdges(e, horzEdge, e.Top);
                if (eMaxPair.OutIdx >= 0) throw 
                  new ClipperException("ProcessHorizontal error");
                return;
              }
              else if(dir == Direction.dLeftToRight)
              {
                IntPoint Pt = new IntPoint(e.Curr.X, horzEdge.Curr.Y);
#if use_xyz
                SetZ(ref Pt, e, horzEdge);
#endif
                IntersectEdges(horzEdge, e, Pt, true);
              }
              else
              {
                IntPoint Pt = new IntPoint(e.Curr.X, horzEdge.Curr.Y);
#if use_xyz
                SetZ(ref Pt, e, horzEdge);
#endif
                IntersectEdges(e, horzEdge, Pt, true);
              }
              SwapPositionsInAEL(horzEdge, e);
            }
            else if ((dir == Direction.dLeftToRight && e.Curr.X >= horzRight) ||
              (dir == Direction.dRightToLeft && e.Curr.X <= horzLeft)) break;
            e = eNext;
          } //end while

          if (horzEdge.OutIdx >= 0 && horzEdge.WindDelta != 0)
            PrepareHorzJoins(horzEdge, isTopOfScanbeam);

          if (horzEdge.NextInLML != null && IsHorizontal(horzEdge.NextInLML))
          {
            UpdateEdgeIntoAEL(ref horzEdge);
            if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Bot);
            GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);
          } else
            break;
        } //end for (;;)

        if(horzEdge.NextInLML != null)
        {
          if(horzEdge.OutIdx >= 0)
          {
            OutPt op1 = AddOutPt( horzEdge, horzEdge.Top);
            UpdateEdgeIntoAEL(ref horzEdge);
            if (horzEdge.WindDelta == 0) return;
            //nb: HorzEdge is no longer horizontal here
            TEdge ePrev = horzEdge.PrevInAEL;
            TEdge eNext = horzEdge.NextInAEL;
            if (ePrev != null && ePrev.Curr.X == horzEdge.Bot.X &&
              ePrev.Curr.Y == horzEdge.Bot.Y && ePrev.WindDelta != 0 &&
              (ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
              SlopesEqual(horzEdge, ePrev, m_UseFullRange)))
            {
              OutPt op2 = AddOutPt(ePrev, horzEdge.Bot);
              AddJoin(op1, op2, horzEdge.Top);
            }
            else if (eNext != null && eNext.Curr.X == horzEdge.Bot.X &&
              eNext.Curr.Y == horzEdge.Bot.Y && eNext.WindDelta != 0 &&
              eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
              SlopesEqual(horzEdge, eNext, m_UseFullRange))
            {
              OutPt op2 = AddOutPt(eNext, horzEdge.Bot);
              AddJoin(op1, op2, horzEdge.Top);
            }
          }
          else
            UpdateEdgeIntoAEL(ref horzEdge); 
        }
        else if (eMaxPair != null)
        {
          if (eMaxPair.OutIdx >= 0)
          {
            if (dir == Direction.dLeftToRight)
              IntersectEdges(horzEdge, eMaxPair, horzEdge.Top); 
            else
              IntersectEdges(eMaxPair, horzEdge, horzEdge.Top);
            if (eMaxPair.OutIdx >= 0) throw 
              new ClipperException("ProcessHorizontal error");
          } else
          {
            DeleteFromAEL(horzEdge);
            DeleteFromAEL(eMaxPair);
          }
        } else
        {
          if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Top);
          DeleteFromAEL(horzEdge);
        }
      }
      //------------------------------------------------------------------------------

      private TEdge GetNextInAEL(TEdge e, Direction Direction)
      {
          return Direction == Direction.dLeftToRight ? e.NextInAEL: e.PrevInAEL;
      }
      //------------------------------------------------------------------------------

      private bool IsMinima(TEdge e)
      {
          return e != null && (e.Prev.NextInLML != e) && (e.Next.NextInLML != e);
      }
      //------------------------------------------------------------------------------

      private bool IsMaxima(TEdge e, double Y)
      {
          return (e != null && e.Top.Y == Y && e.NextInLML == null);
      }
      //------------------------------------------------------------------------------

      private bool IsIntermediate(TEdge e, double Y)
      {
          return (e.Top.Y == Y && e.NextInLML != null);
      }
      //------------------------------------------------------------------------------

      private TEdge GetMaximaPair(TEdge e)
      {
        TEdge result = null;
        if ((e.Next.Top == e.Top) && e.Next.NextInLML == null)
          result = e.Next;
        else if ((e.Prev.Top == e.Top) && e.Prev.NextInLML == null)
          result = e.Prev;
        if (result != null && (result.OutIdx == Skip ||
          (result.NextInAEL == result.PrevInAEL && !IsHorizontal(result))))
          return null;
        return result;
      }
      //------------------------------------------------------------------------------

      private bool ProcessIntersections(cInt botY, cInt topY)
      {
        if( m_ActiveEdges == null ) return true;
        try {
          BuildIntersectList(botY, topY);
          if ( m_IntersectNodes == null) return true;
          if (m_IntersectNodes.Next == null || FixupIntersectionOrder()) 
              ProcessIntersectList();
          else 
              return false;
        }
        catch {
          m_SortedEdges = null;
          DisposeIntersectNodes();
          throw new ClipperException("ProcessIntersections error");
        }
        m_SortedEdges = null;
        return true;
      }
      //------------------------------------------------------------------------------

      private void BuildIntersectList(cInt botY, cInt topY)
      {
        if ( m_ActiveEdges == null ) return;

        //prepare for sorting ...
        TEdge e = m_ActiveEdges;
        m_SortedEdges = e;
        while( e != null )
        {
          e.PrevInSEL = e.PrevInAEL;
          e.NextInSEL = e.NextInAEL;
          e.Curr.X = TopX( e, topY );
          e = e.NextInAEL;
        }

        //bubblesort ...
        bool isModified = true;
        while( isModified && m_SortedEdges != null )
        {
          isModified = false;
          e = m_SortedEdges;
          while( e.NextInSEL != null )
          {
            TEdge eNext = e.NextInSEL;
            IntPoint pt;
            if (e.Curr.X > eNext.Curr.X)
            {
                if (!IntersectPoint(e, eNext, out pt) && e.Curr.X > eNext.Curr.X +1)
                    throw new ClipperException("Intersection error");
                if (pt.Y > botY)
                {
                    pt.Y = botY;
                    if (Math.Abs(e.Dx) > Math.Abs(eNext.Dx))
                      pt.X = TopX(eNext, botY); else
                      pt.X = TopX(e, botY);
                }
#if use_xyz
                SetZ(ref pt, e, eNext);
#endif
                InsertIntersectNode(e, eNext, pt);
                SwapPositionsInSEL(e, eNext);
                isModified = true;
            }
            else
              e = eNext;
          }
          if( e.PrevInSEL != null ) e.PrevInSEL.NextInSEL = null;
          else break;
        }
        m_SortedEdges = null;
      }
      //------------------------------------------------------------------------------

      private bool EdgesAdjacent(IntersectNode inode)
      {
        return (inode.Edge1.NextInSEL == inode.Edge2) ||
          (inode.Edge1.PrevInSEL == inode.Edge2);
      }
      //------------------------------------------------------------------------------

      private bool FixupIntersectionOrder()
      {
          //pre-condition: intersections are sorted bottom-most (then left-most) first.
          //Now it's crucial that intersections are made only between adjacent edges,
          //so to ensure this the order of intersections may need adjusting ...
          IntersectNode inode = m_IntersectNodes;
          CopyAELToSEL();
          while (inode != null)
          {
              if (!EdgesAdjacent(inode))
              {
                  IntersectNode nextNode = inode.Next;
                  while (nextNode != null && !EdgesAdjacent(nextNode))
                      nextNode = nextNode.Next;
                  if (nextNode == null)
                      return false;
                  SwapIntersectNodes(inode, nextNode);
              }
              SwapPositionsInSEL(inode.Edge1, inode.Edge2);
              inode = inode.Next;
          }
          return true;
      }
      //------------------------------------------------------------------------------

      private void ProcessIntersectList()
      {
          while (m_IntersectNodes != null)
          {
          IntersectNode iNode = m_IntersectNodes.Next;
          {
            IntersectEdges( m_IntersectNodes.Edge1 ,
              m_IntersectNodes.Edge2 , m_IntersectNodes.Pt, true);
            SwapPositionsInAEL( m_IntersectNodes.Edge1 , m_IntersectNodes.Edge2 );
          }
          m_IntersectNodes = null;
          m_IntersectNodes = iNode;
        }
      }
      //------------------------------------------------------------------------------

      internal static cInt Round(double value)
      {
          return value < 0 ? (cInt)(value - 0.5) : (cInt)(value + 0.5);
      }
      //------------------------------------------------------------------------------

      private static cInt TopX(TEdge edge, cInt currentY)
      {
          if (currentY == edge.Top.Y)
              return edge.Top.X;
          return edge.Bot.X + Round(edge.Dx *(currentY - edge.Bot.Y));
      }
      //------------------------------------------------------------------------------

      private void InsertIntersectNode(TEdge e1, TEdge e2, IntPoint pt)
      {
        IntersectNode newNode = new IntersectNode();
        newNode.Edge1 = e1;
        newNode.Edge2 = e2;
        newNode.Pt = pt;
        newNode.Next = null;
        if (m_IntersectNodes == null) m_IntersectNodes = newNode;
        else if (newNode.Pt.Y > m_IntersectNodes.Pt.Y)
        {
          newNode.Next = m_IntersectNodes;
          m_IntersectNodes = newNode;
        }
        else
        {
          IntersectNode iNode = m_IntersectNodes;
          while (iNode.Next != null && newNode.Pt.Y < iNode.Next.Pt.Y)
              iNode = iNode.Next;
          newNode.Next = iNode.Next;
          iNode.Next = newNode;
        }
      }
      //------------------------------------------------------------------------------

      private void SwapIntersectNodes(IntersectNode int1, IntersectNode int2)
      {
          TEdge e1 = int1.Edge1;
          TEdge e2 = int1.Edge2;
          IntPoint p = new IntPoint(int1.Pt);
          int1.Edge1 = int2.Edge1;
          int1.Edge2 = int2.Edge2;
          int1.Pt = int2.Pt;
          int2.Edge1 = e1;
          int2.Edge2 = e2;
          int2.Pt = p;
      }
      //------------------------------------------------------------------------------

      private bool IntersectPoint(TEdge edge1, TEdge edge2, out IntPoint ip)
      {
        ip = new IntPoint();
        double b1, b2;
        if (SlopesEqual(edge1, edge2, m_UseFullRange))
        {
            if (edge2.Bot.Y > edge1.Bot.Y)
              ip.Y = edge2.Bot.Y;
            else
              ip.Y = edge1.Bot.Y;
            return false;
        }
        else if (edge1.Delta.X == 0)
        {
            ip.X = edge1.Bot.X;
            if (IsHorizontal(edge2))
            {
                ip.Y = edge2.Bot.Y;
            }
            else
            {
                b2 = edge2.Bot.Y - (edge2.Bot.X / edge2.Dx);
                ip.Y = Round(ip.X / edge2.Dx + b2);
            }
        }
        else if (edge2.Delta.X == 0)
        {
            ip.X = edge2.Bot.X;
            if (IsHorizontal(edge1))
            {
                ip.Y = edge1.Bot.Y;
            }
            else
            {
                b1 = edge1.Bot.Y - (edge1.Bot.X / edge1.Dx);
                ip.Y = Round(ip.X / edge1.Dx + b1);
            }
        }
        else
        {
            b1 = edge1.Bot.X - edge1.Bot.Y * edge1.Dx;
            b2 = edge2.Bot.X - edge2.Bot.Y * edge2.Dx;
            double q = (b2 - b1) / (edge1.Dx - edge2.Dx);
            ip.Y = Round(q);
            if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                ip.X = Round(edge1.Dx * q + b1);
            else
                ip.X = Round(edge2.Dx * q + b2);
        }

        if (ip.Y < edge1.Top.Y || ip.Y < edge2.Top.Y)
        {
            if (edge1.Top.Y > edge2.Top.Y)
            {
                ip.Y = edge1.Top.Y;
                ip.X = TopX(edge2, edge1.Top.Y);
                return ip.X < edge1.Top.X;
            }
            else
            {
                ip.Y = edge2.Top.Y;
                ip.X = TopX(edge1, edge2.Top.Y);
                return ip.X > edge2.Top.X;
            }
        }
        else
            return true;
      }
      //------------------------------------------------------------------------------

      private void DisposeIntersectNodes()
      {
        while ( m_IntersectNodes != null )
        {
          IntersectNode iNode = m_IntersectNodes.Next;
          m_IntersectNodes = null;
          m_IntersectNodes = iNode;
        }
      }
      //------------------------------------------------------------------------------

      private void ProcessEdgesAtTopOfScanbeam(cInt topY)
      {
        TEdge e = m_ActiveEdges;
        while(e != null)
        {
          //1. process maxima, treating them as if they're 'bent' horizontal edges,
          //   but exclude maxima with horizontal edges. nb: e can't be a horizontal.
          bool IsMaximaEdge = IsMaxima(e, topY);

          if(IsMaximaEdge)
          {
            TEdge eMaxPair = GetMaximaPair(e);
            IsMaximaEdge = (eMaxPair == null || !IsHorizontal(eMaxPair));
          }

          if(IsMaximaEdge)
          {
            TEdge ePrev = e.PrevInAEL;
            DoMaxima(e);
            if( ePrev == null) e = m_ActiveEdges;
            else e = ePrev.NextInAEL;
          }
          else
          {
            //2. promote horizontal edges, otherwise update Curr.X and Curr.Y ...
            if (IsIntermediate(e, topY) && IsHorizontal(e.NextInLML))
            {
              UpdateEdgeIntoAEL(ref e);
              if (e.OutIdx >= 0)
                AddOutPt(e, e.Bot);
              AddEdgeToSEL(e);
            } 
            else
            {
              e.Curr.X = TopX( e, topY );
              e.Curr.Y = topY;
            }

            if (StrictlySimple)
            {  
              TEdge ePrev = e.PrevInAEL;
              if ((e.OutIdx >= 0) && (e.WindDelta != 0) && ePrev != null && 
                (ePrev.OutIdx >= 0) && (ePrev.Curr.X == e.Curr.X) && 
                (ePrev.WindDelta != 0))
              {
                IntPoint Pt = e.Curr;
#if use_xyz
                GetZ(ref Pt, ePrev);
                OutPt op = AddOutPt(ePrev, Pt);
                GetZ(ref e.Curr, e);
#else
                OutPt op = AddOutPt(ePrev, Pt);
#endif
                OutPt op2 = AddOutPt(e, Pt);
                AddJoin(op, op2, Pt); //StrictlySimple (type-3) join
              }
            }

            e = e.NextInAEL;
          }
        }

        //3. Process horizontals at the Top of the scanbeam ...
        ProcessHorizontals(true);

        //4. Promote intermediate vertices ...
        e = m_ActiveEdges;
        while (e != null)
        {
          if(IsIntermediate(e, topY))
          {
            OutPt op = null;
            if( e.OutIdx >= 0 ) 
              op = AddOutPt(e, e.Top);
            UpdateEdgeIntoAEL(ref e);

            //if output polygons share an edge, they'll need joining later ...
            TEdge ePrev = e.PrevInAEL;
            TEdge eNext = e.NextInAEL;
            if (ePrev != null && ePrev.Curr.X == e.Bot.X &&
              ePrev.Curr.Y == e.Bot.Y && op != null &&
              ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
              SlopesEqual(e, ePrev, m_UseFullRange) &&
              (e.WindDelta != 0) && (ePrev.WindDelta != 0))
            {
              OutPt op2 = AddOutPt(ePrev, e.Bot);
              AddJoin(op, op2, e.Top);
            }
            else if (eNext != null && eNext.Curr.X == e.Bot.X &&
              eNext.Curr.Y == e.Bot.Y && op != null &&
              eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
              SlopesEqual(e, eNext, m_UseFullRange) &&
              (e.WindDelta != 0) && (eNext.WindDelta != 0))
            {
              OutPt op2 = AddOutPt(eNext, e.Bot);
              AddJoin(op, op2, e.Top);
            }
          }
          e = e.NextInAEL;
        }
      }
      //------------------------------------------------------------------------------

      private void DoMaxima(TEdge e)
      {
        TEdge eMaxPair = GetMaximaPair(e);
        if (eMaxPair == null)
        {
          if (e.OutIdx >= 0)
            AddOutPt(e, e.Top);
          DeleteFromAEL(e);
          return;
        }

        TEdge eNext = e.NextInAEL;
        while(eNext != null && eNext != eMaxPair)
        {
          IntPoint Pt = e.Top;
#if use_xyz
          SetZ(ref Pt, e, eNext);
#endif
          IntersectEdges(e, eNext, Pt, true);
          SwapPositionsInAEL(e, eNext);
          eNext = e.NextInAEL;
        }

        if(e.OutIdx == Unassigned && eMaxPair.OutIdx == Unassigned)
        {
          DeleteFromAEL(e);
          DeleteFromAEL(eMaxPair);
        }
        else if( e.OutIdx >= 0 && eMaxPair.OutIdx >= 0 )
        {
          IntersectEdges( e, eMaxPair, e.Top);
        }
#if use_lines
        else if (e.WindDelta == 0)
        {
          if (e.OutIdx >= 0) 
          {
            AddOutPt(e, e.Top);
            e.OutIdx = Unassigned;
          }
          DeleteFromAEL(e);

          if (eMaxPair.OutIdx >= 0)
          {
            AddOutPt(eMaxPair, e.Top);
            eMaxPair.OutIdx = Unassigned;
          }
          DeleteFromAEL(eMaxPair);
        } 
#endif
        else throw new ClipperException("DoMaxima error");
      }
      //------------------------------------------------------------------------------

      public static void ReversePaths(Paths polys)
      {
        polys.ForEach(delegate(Path poly) { poly.Reverse(); });
      }
      //------------------------------------------------------------------------------

      public static bool Orientation(Path poly)
      {
          return Area(poly) >= 0;
      }
      //------------------------------------------------------------------------------

      private int PointCount(OutPt pts)
      {
          if (pts == null) return 0;
          int result = 0;
          OutPt p = pts;
          do
          {
              result++;
              p = p.Next;
          }
          while (p != pts);
          return result;
      }
      //------------------------------------------------------------------------------

      private void BuildResult(Paths polyg)
      {
          polyg.Clear();
          polyg.Capacity = m_PolyOuts.Count;
          for (int i = 0; i < m_PolyOuts.Count; i++)
          {
              OutRec outRec = m_PolyOuts[i];
              if (outRec.Pts == null) continue;
              OutPt p = outRec.Pts;
              int cnt = PointCount(p);
              if (cnt < 2) continue;
              Path pg = new Path(cnt);
              for (int j = 0; j < cnt; j++)
              {
                  pg.Add(p.Pt);
                  p = p.Prev;
              }
              polyg.Add(pg);
          }
      }
      //------------------------------------------------------------------------------

      private void BuildResult2(PolyTree polytree)
      {
          polytree.Clear();

          //add each output polygon/contour to polytree ...
          polytree.m_AllPolys.Capacity = m_PolyOuts.Count;
          for (int i = 0; i < m_PolyOuts.Count; i++)
          {
              OutRec outRec = m_PolyOuts[i];
              int cnt = PointCount(outRec.Pts);
              if ((outRec.IsOpen && cnt < 2) || 
                (!outRec.IsOpen && cnt < 3)) continue;
              FixHoleLinkage(outRec);
              PolyNode pn = new PolyNode();
              polytree.m_AllPolys.Add(pn);
              outRec.PolyNode = pn;
              pn.m_polygon.Capacity = cnt;
              OutPt op = outRec.Pts.Prev;
              for (int j = 0; j < cnt; j++)
              {
                  pn.m_polygon.Add(op.Pt);
                  op = op.Prev;
              }
          }

          //fixup PolyNode links etc ...
          polytree.m_Childs.Capacity = m_PolyOuts.Count;
          for (int i = 0; i < m_PolyOuts.Count; i++)
          {
              OutRec outRec = m_PolyOuts[i];
              if (outRec.PolyNode == null) continue;
              else if (outRec.IsOpen)
              {
                outRec.PolyNode.IsOpen = true;
                polytree.AddChild(outRec.PolyNode);
              }
              else if (outRec.FirstLeft != null)
                outRec.FirstLeft.PolyNode.AddChild(outRec.PolyNode);
              else
                polytree.AddChild(outRec.PolyNode);
          }
      }
      //------------------------------------------------------------------------------

      private void FixupOutPolygon(OutRec outRec)
      {
          //FixupOutPolygon() - removes duplicate points and simplifies consecutive
          //parallel edges by removing the middle vertex.
          OutPt lastOK = null;
          outRec.BottomPt = null;
          OutPt pp = outRec.Pts;
          for (;;)
          {
              if (pp.Prev == pp || pp.Prev == pp.Next)
              {
                  DisposeOutPts(pp);
                  outRec.Pts = null;
                  return;
              }
              //test for duplicate points and collinear edges ...
              if ((pp.Pt == pp.Next.Pt) || (pp.Pt == pp.Prev.Pt) ||
                (SlopesEqual(pp.Prev.Pt, pp.Pt, pp.Next.Pt, m_UseFullRange) &&
                (!PreserveCollinear || !Pt2IsBetweenPt1AndPt3(pp.Prev.Pt, pp.Pt, pp.Next.Pt))))
              {
                  lastOK = null;
                  OutPt tmp = pp;
                  pp.Prev.Next = pp.Next;
                  pp.Next.Prev = pp.Prev;
                  pp = pp.Prev;
                  tmp = null;
              }
              else if (pp == lastOK) break;
              else
              {
                  if (lastOK == null) lastOK = pp;
                  pp = pp.Next;
              }
          }
          outRec.Pts = pp;
      }
      //------------------------------------------------------------------------------

      OutPt DupOutPt(OutPt outPt, bool InsertAfter)
      {
        OutPt result = new OutPt();
        result.Pt = outPt.Pt;
        result.Idx = outPt.Idx;
        if (InsertAfter)
        {
          result.Next = outPt.Next;
          result.Prev = outPt;
          outPt.Next.Prev = result;
          outPt.Next = result;
        } 
        else
        {
          result.Prev = outPt.Prev;
          result.Next = outPt;
          outPt.Prev.Next = result;
          outPt.Prev = result;
        }
        return result;
      }
      //------------------------------------------------------------------------------

      bool GetOverlap(cInt a1, cInt a2, cInt b1, cInt b2, out cInt Left, out cInt Right)
      {
        if (a1 < a2)
        {
          if (b1 < b2) {Left = Math.Max(a1,b1); Right = Math.Min(a2,b2);}
          else {Left = Math.Max(a1,b2); Right = Math.Min(a2,b1);}
        } 
        else
        {
          if (b1 < b2) {Left = Math.Max(a2,b1); Right = Math.Min(a1,b2);}
          else { Left = Math.Max(a2, b2); Right = Math.Min(a1, b1); }
        }
        return Left < Right;
      }
      //------------------------------------------------------------------------------

      bool JoinHorz(OutPt op1, OutPt op1b, OutPt op2, OutPt op2b, 
        IntPoint Pt, bool DiscardLeft)
      {
        Direction Dir1 = (op1.Pt.X > op1b.Pt.X ? 
          Direction.dRightToLeft : Direction.dLeftToRight);
        Direction Dir2 = (op2.Pt.X > op2b.Pt.X ?
          Direction.dRightToLeft : Direction.dLeftToRight);
        if (Dir1 == Dir2) return false;

        //When DiscardLeft, we want Op1b to be on the Left of Op1, otherwise we
        //want Op1b to be on the Right. (And likewise with Op2 and Op2b.)
        //So, to facilitate this while inserting Op1b and Op2b ...
        //when DiscardLeft, make sure we're AT or RIGHT of Pt before adding Op1b,
        //otherwise make sure we're AT or LEFT of Pt. (Likewise with Op2b.)
        if (Dir1 == Direction.dLeftToRight) 
        {
          while (op1.Next.Pt.X <= Pt.X && 
            op1.Next.Pt.X >= op1.Pt.X && op1.Next.Pt.Y == Pt.Y)  
              op1 = op1.Next;
          if (DiscardLeft && (op1.Pt.X != Pt.X)) op1 = op1.Next;
          op1b = DupOutPt(op1, !DiscardLeft);
          if (op1b.Pt != Pt) 
          {
            op1 = op1b;
            op1.Pt = Pt;
            op1b = DupOutPt(op1, !DiscardLeft);
          }
        } 
        else
        {
          while (op1.Next.Pt.X >= Pt.X && 
            op1.Next.Pt.X <= op1.Pt.X && op1.Next.Pt.Y == Pt.Y) 
              op1 = op1.Next;
          if (!DiscardLeft && (op1.Pt.X != Pt.X)) op1 = op1.Next;
          op1b = DupOutPt(op1, DiscardLeft);
          if (op1b.Pt != Pt)
          {
            op1 = op1b;
            op1.Pt = Pt;
            op1b = DupOutPt(op1, DiscardLeft);
          }
        }

        if (Dir2 == Direction.dLeftToRight)
        {
          while (op2.Next.Pt.X <= Pt.X && 
            op2.Next.Pt.X >= op2.Pt.X && op2.Next.Pt.Y == Pt.Y)
              op2 = op2.Next;
          if (DiscardLeft && (op2.Pt.X != Pt.X)) op2 = op2.Next;
          op2b = DupOutPt(op2, !DiscardLeft);
          if (op2b.Pt != Pt)
          {
            op2 = op2b;
            op2.Pt = Pt;
            op2b = DupOutPt(op2, !DiscardLeft);
          };
        } else
        {
          while (op2.Next.Pt.X >= Pt.X && 
            op2.Next.Pt.X <= op2.Pt.X && op2.Next.Pt.Y == Pt.Y) 
              op2 = op2.Next;
          if (!DiscardLeft && (op2.Pt.X != Pt.X)) op2 = op2.Next;
          op2b = DupOutPt(op2, DiscardLeft);
          if (op2b.Pt != Pt)
          {
            op2 = op2b;
            op2.Pt = Pt;
            op2b = DupOutPt(op2, DiscardLeft);
          };
        };

        if ((Dir1 == Direction.dLeftToRight) == DiscardLeft)
        {
          op1.Prev = op2;
          op2.Next = op1;
          op1b.Next = op2b;
          op2b.Prev = op1b;
        }
        else
        {
          op1.Next = op2;
          op2.Prev = op1;
          op1b.Prev = op2b;
          op2b.Next = op1b;
        }
        return true;
      }
      //------------------------------------------------------------------------------

      private bool JoinPoints(Join j, out OutPt p1, out OutPt p2)
      {
        OutRec outRec1 = GetOutRec(j.OutPt1.Idx);
        OutRec outRec2 = GetOutRec(j.OutPt2.Idx);
        OutPt op1 = j.OutPt1, op1b;
        OutPt op2 = j.OutPt2, op2b;
        p1 = null; p2 = null;

        //There are 3 kinds of joins for output polygons ...
        //1. Horizontal joins where Join.OutPt1 & Join.OutPt2 are a vertices anywhere
        //along (horizontal) collinear edges (& Join.OffPt is on the same horizontal).
        //2. Non-horizontal joins where Join.OutPt1 & Join.OutPt2 are at the same
        //location at the Bottom of the overlapping segment (& Join.OffPt is above).
        //3. StrictSimple joins where edges touch but are not collinear and where
        //Join.OutPt1, Join.OutPt2 & Join.OffPt all share the same point.
        bool isHorizontal = (j.OutPt1.Pt.Y == j.OffPt.Y);

        if (isHorizontal && (j.OffPt == j.OutPt1.Pt) && (j.OffPt == j.OutPt2.Pt))
        {
          //Strictly Simple join ...
          op1b = j.OutPt1.Next;
          while (op1b != op1 && (op1b.Pt == j.OffPt)) 
            op1b = op1b.Next;
          bool reverse1 = (op1b.Pt.Y > j.OffPt.Y);
          op2b = j.OutPt2.Next;
          while (op2b != op2 && (op2b.Pt == j.OffPt)) 
            op2b = op2b.Next;
          bool reverse2 = (op2b.Pt.Y > j.OffPt.Y);
          if (reverse1 == reverse2) return false;
          if (reverse1)
          {
            op1b = DupOutPt(op1, false);
            op2b = DupOutPt(op2, true);
            op1.Prev = op2;
            op2.Next = op1;
            op1b.Next = op2b;
            op2b.Prev = op1b;
            p1 = op1;
            p2 = op1b;
            return true;
          } else
          {
            op1b = DupOutPt(op1, true);
            op2b = DupOutPt(op2, false);
            op1.Next = op2;
            op2.Prev = op1;
            op1b.Prev = op2b;
            op2b.Next = op1b;
            p1 = op1;
            p2 = op1b;
            return true;
          }
        } 
        else if (isHorizontal)
        {
          op1 = j.OutPt1; op1b = j.OutPt1;
          while (op1.Prev.Pt.Y == op1.Pt.Y && op1.Prev != j.OutPt1)
            op1 = op1.Prev;
          while (op1b.Next.Pt.Y == op1b.Pt.Y && op1b.Next != j.OutPt1)
            op1b = op1b.Next;
          if (op1.Pt.X == op1b.Pt.X) return false; //todo - test if this ever happens

          op2 = j.OutPt2; op2b = j.OutPt2;
          while (op2.Prev.Pt.Y == op2.Pt.Y && op2.Prev != j.OutPt2)
            op2 = op2.Prev;
          while (op2b.Next.Pt.Y == op2b.Pt.Y && op2b.Next != j.OutPt2)
            op2b = op2b.Next;
          if (op2.Pt.X == op2b.Pt.X) return false; //todo - test if this ever happens

          cInt Left, Right;
          //Op1 -. Op1b & Op2 -. Op2b are the extremites of the horizontal edges
          if (!GetOverlap(op1.Pt.X, op1b.Pt.X, op2.Pt.X, op2b.Pt.X, out Left, out Right))
            return false;

          //DiscardLeftSide: when overlapping edges are joined, a spike will created
          //which needs to be cleaned up. However, we don't want Op1 or Op2 caught up
          //on the discard Side as either may still be needed for other joins ...
          IntPoint Pt;
          bool DiscardLeftSide;
          if (op1.Pt.X >= Left && op1.Pt.X <= Right) 
          {
            Pt = op1.Pt; DiscardLeftSide = (op1.Pt.X > op1b.Pt.X);
          } 
          else if (op2.Pt.X >= Left&& op2.Pt.X <= Right) 
          {
            Pt = op2.Pt; DiscardLeftSide = (op2.Pt.X > op2b.Pt.X);
          } 
          else if (op1b.Pt.X >= Left && op1b.Pt.X <= Right)
          {
            Pt = op1b.Pt; DiscardLeftSide = op1b.Pt.X > op1.Pt.X;
          } 
          else
          {
            Pt = op2b.Pt; DiscardLeftSide = (op2b.Pt.X > op2.Pt.X);
          }
          p1 = op1; p2 = op2;
          return JoinHorz(op1, op1b, op2, op2b, Pt, DiscardLeftSide);
        } else
        {
          //nb: For non-horizontal joins ...
          //    1. Jr.OutPt1.Pt.Y == Jr.OutPt2.Pt.Y
          //    2. Jr.OutPt1.Pt > Jr.OffPt.Y

          //make sure the polygons are correctly oriented ...
          op1b = op1.Next;
          while ((op1b.Pt == op1.Pt) && (op1b != op1)) op1b = op1b.Next;
          bool Reverse1 = ((op1b.Pt.Y > op1.Pt.Y) ||
            !SlopesEqual(op1.Pt, op1b.Pt, j.OffPt, m_UseFullRange));
          if (Reverse1)
          {
            op1b = op1.Prev;
            while ((op1b.Pt == op1.Pt) && (op1b != op1)) op1b = op1b.Prev;
            if ((op1b.Pt.Y > op1.Pt.Y) ||
              !SlopesEqual(op1.Pt, op1b.Pt, j.OffPt, m_UseFullRange)) return false;
          };
          op2b = op2.Next;
          while ((op2b.Pt == op2.Pt) && (op2b != op2)) op2b = op2b.Next;
          bool Reverse2 = ((op2b.Pt.Y > op2.Pt.Y) ||
            !SlopesEqual(op2.Pt, op2b.Pt, j.OffPt, m_UseFullRange));
          if (Reverse2)
          {
            op2b = op2.Prev;
            while ((op2b.Pt == op2.Pt) && (op2b != op2)) op2b = op2b.Prev;
            if ((op2b.Pt.Y > op2.Pt.Y) ||
              !SlopesEqual(op2.Pt, op2b.Pt, j.OffPt, m_UseFullRange)) return false;
          }

          if ((op1b == op1) || (op2b == op2) || (op1b == op2b) ||
            ((outRec1 == outRec2) && (Reverse1 == Reverse2))) return false;

          if (Reverse1)
          {
            op1b = DupOutPt(op1, false);
            op2b = DupOutPt(op2, true);
            op1.Prev = op2;
            op2.Next = op1;
            op1b.Next = op2b;
            op2b.Prev = op1b;
            p1 = op1;
            p2 = op1b;
            return true;
          } else
          {
            op1b = DupOutPt(op1, true);
            op2b = DupOutPt(op2, false);
            op1.Next = op2;
            op2.Prev = op1;
            op1b.Prev = op2b;
            op2b.Next = op1b;
            p1 = op1;
            p2 = op1b;
            return true;
          }
        }
      }
      //----------------------------------------------------------------------


      private bool Poly2ContainsPoly1(OutPt outPt1, OutPt outPt2, bool UseFullInt64Range)
      {
          OutPt pt = outPt1;
          //Because the polygons may be touching, we need to find a vertex that
          //isn't touching the other polygon ...
          if (PointOnPolygon(pt.Pt, outPt2, UseFullInt64Range))
          {
              pt = pt.Next;
              while (pt != outPt1 && PointOnPolygon(pt.Pt, outPt2, UseFullInt64Range))
                  pt = pt.Next;
              if (pt == outPt1) return true;
          }
          return PointInPolygon(pt.Pt, outPt2, UseFullInt64Range);
      }
      //----------------------------------------------------------------------

      private void FixupFirstLefts1(OutRec OldOutRec, OutRec NewOutRec)
      { 
          for (int i = 0; i < m_PolyOuts.Count; i++)
          {
              OutRec outRec = m_PolyOuts[i];
              if (outRec.Pts != null && outRec.FirstLeft == OldOutRec) 
              {
                  if (Poly2ContainsPoly1(outRec.Pts, NewOutRec.Pts, m_UseFullRange))
                      outRec.FirstLeft = NewOutRec;
              }
          }
      }
      //----------------------------------------------------------------------

      private void FixupFirstLefts2(OutRec OldOutRec, OutRec NewOutRec)
      { 
          foreach (OutRec outRec in m_PolyOuts)
              if (outRec.FirstLeft == OldOutRec) outRec.FirstLeft = NewOutRec;
      }
      //----------------------------------------------------------------------

      private void JoinCommonEdges()
      {
        for (int i = 0; i < m_Joins.Count; i++)
        {
          Join j = m_Joins[i];

          OutRec outRec1 = GetOutRec(j.OutPt1.Idx);
          OutRec outRec2 = GetOutRec(j.OutPt2.Idx);

          if (outRec1.Pts == null || outRec2.Pts == null) continue;

          //get the polygon fragment with the correct hole state (FirstLeft)
          //before calling JoinPoints() ...
          OutRec holeStateRec;
          if (outRec1 == outRec2) holeStateRec = outRec1;
          else if (Param1RightOfParam2(outRec1, outRec2)) holeStateRec = outRec2;
          else if (Param1RightOfParam2(outRec2, outRec1)) holeStateRec = outRec1;
          else holeStateRec = GetLowermostRec(outRec1, outRec2);

          OutPt p1, p2;
          if (!JoinPoints(j, out p1, out p2)) continue;

          if (outRec1 == outRec2)
          {
            //instead of joining two polygons, we've just created a new one by
            //splitting one polygon into two.
            outRec1.Pts = p1;
            outRec1.BottomPt = null;
            outRec2 = CreateOutRec();
            outRec2.Pts = p2;

            //update all OutRec2.Pts Idx's ...
            UpdateOutPtIdxs(outRec2);

            if (Poly2ContainsPoly1(outRec2.Pts, outRec1.Pts, m_UseFullRange))
            {
              //outRec2 is contained by outRec1 ...
              outRec2.IsHole = !outRec1.IsHole;
              outRec2.FirstLeft = outRec1;

              //fixup FirstLeft pointers that may need reassigning to OutRec1
              if (m_UsingPolyTree) FixupFirstLefts2(outRec2, outRec1);

              if ((outRec2.IsHole ^ ReverseSolution) == (Area(outRec2) > 0))
                ReversePolyPtLinks(outRec2.Pts);
            
            } else if (Poly2ContainsPoly1(outRec1.Pts, outRec2.Pts, m_UseFullRange))
            {
              //outRec1 is contained by outRec2 ...
              outRec2.IsHole = outRec1.IsHole;
              outRec1.IsHole = !outRec2.IsHole;
              outRec2.FirstLeft = outRec1.FirstLeft;
              outRec1.FirstLeft = outRec2;

              //fixup FirstLeft pointers that may need reassigning to OutRec1
              if (m_UsingPolyTree) FixupFirstLefts2(outRec1, outRec2);

              if ((outRec1.IsHole ^ ReverseSolution) == (Area(outRec1) > 0))
                ReversePolyPtLinks(outRec1.Pts);
            } 
            else
            {
              //the 2 polygons are completely separate ...
              outRec2.IsHole = outRec1.IsHole;
              outRec2.FirstLeft = outRec1.FirstLeft;

              //fixup FirstLeft pointers that may need reassigning to OutRec2
              if (m_UsingPolyTree) FixupFirstLefts1(outRec1, outRec2);
            }
     
          } else
          {
            //joined 2 polygons together ...

            outRec2.Pts = null;
            outRec2.BottomPt = null;
            outRec2.Idx = outRec1.Idx;

            outRec1.IsHole = holeStateRec.IsHole;
            if (holeStateRec == outRec2) 
              outRec1.FirstLeft = outRec2.FirstLeft;
            outRec2.FirstLeft = outRec1;

            //fixup FirstLeft pointers that may need reassigning to OutRec1
            if (m_UsingPolyTree) FixupFirstLefts2(outRec2, outRec1);
          }
        }
      }
      //------------------------------------------------------------------------------

      private void UpdateOutPtIdxs(OutRec outrec)
      {  
        OutPt op = outrec.Pts;
        do
        {
          op.Idx = outrec.Idx;
          op = op.Prev;
        }
        while(op != outrec.Pts);
      }
      //------------------------------------------------------------------------------

      private void DoSimplePolygons()
      {
        int i = 0;
        while (i < m_PolyOuts.Count) 
        {
          OutRec outrec = m_PolyOuts[i++];
          OutPt op = outrec.Pts;
          if (op == null) continue;
          do //for each Pt in Polygon until duplicate found do ...
          {
            OutPt op2 = op.Next;
            while (op2 != outrec.Pts) 
            {
              if ((op.Pt == op2.Pt) && op2.Next != op && op2.Prev != op) 
              {
                //split the polygon into two ...
                OutPt op3 = op.Prev;
                OutPt op4 = op2.Prev;
                op.Prev = op4;
                op4.Next = op;
                op2.Prev = op3;
                op3.Next = op2;

                outrec.Pts = op;
                OutRec outrec2 = CreateOutRec();
                outrec2.Pts = op2;
                UpdateOutPtIdxs(outrec2);
                if (Poly2ContainsPoly1(outrec2.Pts, outrec.Pts, m_UseFullRange))
                {
                  //OutRec2 is contained by OutRec1 ...
                  outrec2.IsHole = !outrec.IsHole;
                  outrec2.FirstLeft = outrec;
                }
                else
                  if (Poly2ContainsPoly1(outrec.Pts, outrec2.Pts, m_UseFullRange))
                {
                  //OutRec1 is contained by OutRec2 ...
                  outrec2.IsHole = outrec.IsHole;
                  outrec.IsHole = !outrec2.IsHole;
                  outrec2.FirstLeft = outrec.FirstLeft;
                  outrec.FirstLeft = outrec2;
                } else
                {
                  //the 2 polygons are separate ...
                  outrec2.IsHole = outrec.IsHole;
                  outrec2.FirstLeft = outrec.FirstLeft;
                }
                op2 = op; //ie get ready for the next iteration
              }
              op2 = op2.Next;
            }
            op = op.Next;
          }
          while (op != outrec.Pts);
        }
      }
      //------------------------------------------------------------------------------

      public static double Area(Path poly)
      {
        int highI = poly.Count - 1;
        if (highI < 2) return 0;
        double area = ((double)poly[highI].X + poly[0].X) * ((double)poly[0].Y - poly[highI].Y);
        for (int i = 1; i <= highI; ++i)
            area += ((double)poly[i - 1].X + poly[i].X) * ((double)poly[i].Y - poly[i -1].Y);
        return area / 2;
      }
      //------------------------------------------------------------------------------

      double Area(OutRec outRec)
      {
        OutPt op = outRec.Pts;
        if (op == null) return 0;
        double a = 0;
        do {
          a = a + (double)(op.Pt.X + op.Prev.Pt.X) * (double)(op.Prev.Pt.Y - op.Pt.Y);
          op = op.Next;
        } while (op != outRec.Pts);
        return a/2;
      }

      //------------------------------------------------------------------------------
      // OffsetPolygon functions ...
      //------------------------------------------------------------------------------

      internal static DoublePoint GetUnitNormal(IntPoint pt1, IntPoint pt2)
      {
          double dx = (pt2.X - pt1.X);
          double dy = (pt2.Y - pt1.Y);
          if ((dx == 0) && (dy == 0)) return new DoublePoint();

          double f = 1 * 1.0 / Math.Sqrt(dx * dx + dy * dy);
          dx *= f;
          dy *= f;

          return new DoublePoint(dy, -dx);
      }
      //------------------------------------------------------------------------------

      private class PolyOffsetBuilder
      {
          private Paths m_p;
          private Path currentPoly;
          private List<DoublePoint> normals = new List<DoublePoint>();
          private double m_delta, m_sinA, m_sin, m_cos;
          private double m_miterLim, m_Steps360;
          private int m_i, m_j, m_k;
          private const int m_buffLength = 128;

          void OffsetPoint(JoinType jointype)
          {
              m_sinA = (normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y);
              if (m_sinA > 1.0) m_sinA = 1.0; else if (m_sinA < -1.0) m_sinA = -1.0;

              if (m_sinA * m_delta < 0)
              {
                  AddPoint(new IntPoint(Round(m_p[m_i][m_j].X + normals[m_k].X * m_delta),
                    Round(m_p[m_i][m_j].Y + normals[m_k].Y * m_delta)));
                  AddPoint(m_p[m_i][m_j]);
                  AddPoint(new IntPoint(Round(m_p[m_i][m_j].X + normals[m_j].X * m_delta),
                    Round(m_p[m_i][m_j].Y + normals[m_j].Y * m_delta)));
              }
              else
                  switch (jointype)
                  {
                      case JoinType.jtMiter:
                          {
                              double r = 1 + (normals[m_j].X * normals[m_k].X +
                                normals[m_j].Y * normals[m_k].Y);
                              if (r >= m_miterLim) DoMiter(r); else DoSquare();
                              break;
                          }
                      case JoinType.jtSquare: DoSquare(); break;
                      case JoinType.jtRound: DoRound(); break;
                  }
              m_k = m_j;
          }
          //------------------------------------------------------------------------------

          public PolyOffsetBuilder(Paths pts, out Paths solution, double delta,
              JoinType jointype, EndType endtype, double limit = 0)
          {
              //precondition: solution != pts
              solution = new Paths();
              if (ClipperBase.near_zero(delta)) {solution = pts; return; }
              m_p = pts;
              if (endtype != EndType.etClosed && delta < 0) delta = -delta;
              m_delta = delta;

              if (jointype == JoinType.jtMiter)
              {
                  //m_miterVal: see offset_triginometry.svg in the documentation folder ...
                  if (limit > 2) m_miterLim = 2 / (limit * limit);
                  else m_miterLim = 0.5;
                  if (endtype == EndType.etRound) limit = 0.25;
              }
              if (jointype == JoinType.jtRound || endtype == EndType.etRound)
              {
              if (limit <= 0) limit = 0.25;
              else if (limit > Math.Abs(delta)*0.25) limit = Math.Abs(delta)*0.25;
              //m_roundVal: see offset_triginometry2.svg in the documentation folder ...
              m_Steps360 = Math.PI / Math.Acos(1 - limit / Math.Abs(delta));
              m_sin = Math.Sin(2 * Math.PI / m_Steps360);
              m_cos = Math.Cos(2 * Math.PI / m_Steps360);
              m_Steps360 /= Math.PI * 2;
              if (delta < 0) m_sin = -m_sin;
              }

              double deltaSq = delta * delta;
              solution.Capacity = pts.Count;
              for (m_i = 0; m_i < pts.Count; m_i++)
              {
                  int len = pts[m_i].Count;
                  if (len == 0 || (len < 3 && delta <= 0)) continue;
                    
                  if (len == 1)
                  {
                      if (jointype == JoinType.jtRound)
                      {
                          double X = 1.0, Y = 0.0;
                          for (cInt j = 1; j <= Round(m_Steps360 * 2 * Math.PI); j++)
                          {
                              AddPoint(new IntPoint(
                                Round(m_p[m_i][0].X + X * delta),
                                Round(m_p[m_i][0].Y + Y * delta)));
                              double X2 = X;
                              X = X * m_cos - m_sin * Y;
                              Y = X2 * m_sin + Y * m_cos;
                          }
                      }
                      else
                      {
                          double X = -1.0, Y = -1.0;
                          for (int j = 0; j < 4; ++j)
                          {
                              AddPoint(new IntPoint(Round(m_p[m_i][0].X + X * delta),
                                Round(m_p[m_i][0].Y + Y * delta)));
                              if (X < 0) X = 1;
                              else if (Y < 0) Y = 1;
                              else X = -1;
                          }
                      }
                      continue;
                  }
                    
                  //build normals ...
                  normals.Clear();
                  normals.Capacity = len;
                  for (int j = 0; j < len -1; ++j)
                      normals.Add(GetUnitNormal(pts[m_i][j], pts[m_i][j+1]));
                  if (endtype == EndType.etClosed)
                      normals.Add(GetUnitNormal(pts[m_i][len - 1], pts[m_i][0]));
                  else
                      normals.Add(new DoublePoint(normals[len - 2]));

                  currentPoly = new Path();
                  if (endtype == EndType.etClosed)
                  {
                      m_k = len - 1;
                      for (m_j = 0; m_j < len; ++m_j)
                          OffsetPoint(jointype);
                      solution.Add(currentPoly); 
                  }
                  else
                  {
                      m_k = 0;
                      for (m_j = 1; m_j < len - 1; ++m_j)
                          OffsetPoint(jointype);

                      IntPoint pt1;
                      if (endtype == EndType.etButt)
                      {
                          m_j = len - 1;
                          pt1 = new IntPoint((cInt)Round(pts[m_i][m_j].X + normals[m_j].X *
                            delta), (cInt)Round(pts[m_i][m_j].Y + normals[m_j].Y * delta));
                          AddPoint(pt1);
                          pt1 = new IntPoint((cInt)Round(pts[m_i][m_j].X - normals[m_j].X *
                            delta), (cInt)Round(pts[m_i][m_j].Y - normals[m_j].Y * delta));
                          AddPoint(pt1);
                      }
                      else
                      {
                          m_j = len - 1;
                          m_k = len - 2;
                          m_sinA = 0;
                          normals[m_j].X = -normals[m_j].X;
                          normals[m_j].Y = -normals[m_j].Y;
                          if (endtype == EndType.etSquare)
                            DoSquare();
                          else
                            DoRound();
                      }

                      //re-build Normals ...
                      for (int j = len - 1; j > 0; j--)
                      {
                          normals[j].X = -normals[j - 1].X;
                          normals[j].Y = -normals[j - 1].Y;
                      }
                      normals[0].X = -normals[1].X;
                      normals[0].Y = -normals[1].Y;

                      m_k = len - 1;
                      for (m_j = m_k - 1; m_j > 0; --m_j)
                          OffsetPoint(jointype);

                      if (endtype == EndType.etButt)
                      {
                          pt1 = new IntPoint((cInt)Round(pts[m_i][0].X - normals[0].X * delta),
                            (cInt)Round(pts[m_i][0].Y - normals[0].Y * delta));
                          AddPoint(pt1);
                          pt1 = new IntPoint((cInt)Round(pts[m_i][0].X + normals[0].X * delta),
                            (cInt)Round(pts[m_i][0].Y + normals[0].Y * delta));
                          AddPoint(pt1);
                      }
                      else
                      {
                          m_k = 1;
                          m_sinA = 0;
                          if (endtype == EndType.etSquare) 
                            DoSquare();
                          else
                            DoRound();
                      }
                      solution.Add(currentPoly);
                  }
              }

              //finally, clean up untidy corners ...
              Clipper clpr = new Clipper();
              clpr.AddPaths(solution, PolyType.ptSubject, true);
              if (delta > 0)
              {
                  clpr.Execute(ClipType.ctUnion, solution, PolyFillType.pftPositive, PolyFillType.pftPositive);
              }
              else
              {
                  IntRect r = clpr.GetBounds();
                  Path outer = new Path(4);

                  outer.Add(new IntPoint(r.left - 10, r.bottom + 10));
                  outer.Add(new IntPoint(r.right + 10, r.bottom + 10));
                  outer.Add(new IntPoint(r.right + 10, r.top - 10));
                  outer.Add(new IntPoint(r.left - 10, r.top - 10));

                  clpr.AddPath(outer, PolyType.ptSubject, true);
                  clpr.ReverseSolution = true;
                  clpr.Execute(ClipType.ctUnion, solution, PolyFillType.pftNegative, PolyFillType.pftNegative);
                  if (solution.Count > 0) solution.RemoveAt(0);
              }
          }
          //------------------------------------------------------------------------------

          internal void AddPoint(IntPoint pt)
          {
              if (currentPoly.Count == currentPoly.Capacity)
                  currentPoly.Capacity += m_buffLength;
              currentPoly.Add(pt);
          }
          //------------------------------------------------------------------------------

          internal void DoSquare()
          {
              double dx = Math.Tan(Math.Atan2(m_sinA, 
                  normals[m_k].X * normals[m_j].X + normals[m_k].Y * normals[m_j].Y)/4);
              AddPoint(new IntPoint(
                  Round(m_p[m_i][m_j].X + m_delta * (normals[m_k].X - normals[m_k].Y *dx)),
                  Round(m_p[m_i][m_j].Y + m_delta * (normals[m_k].Y + normals[m_k].X *dx))));
              AddPoint(new IntPoint(
                  Round(m_p[m_i][m_j].X + m_delta * (normals[m_j].X + normals[m_j].Y *dx)),
                  Round(m_p[m_i][m_j].Y + m_delta * (normals[m_j].Y - normals[m_j].X *dx))));                
          }
          //------------------------------------------------------------------------------

          internal void DoMiter(double r)
          {
              double q = m_delta / r;
              AddPoint(new IntPoint(Round(m_p[m_i][m_j].X + (normals[m_k].X + normals[m_j].X) * q),
                  Round(m_p[m_i][m_j].Y + (normals[m_k].Y + normals[m_j].Y) * q)));
          }
          //------------------------------------------------------------------------------

          internal void DoRound()
          {
              double a = Math.Atan2(m_sinA, 
              normals[m_k].X * normals[m_j].X + normals[m_k].Y * normals[m_j].Y);
              int steps = (int)Round(m_Steps360 * Math.Abs(a));

              double X = normals[m_k].X, Y = normals[m_k].Y, X2;
              for (int i = 0; i < steps; ++i)
              {
                  AddPoint(new IntPoint(
                      Round(m_p[m_i][m_j].X + X * m_delta),
                      Round(m_p[m_i][m_j].Y + Y * m_delta)));
                  X2 = X;
                  X = X * m_cos - m_sin * Y;
                  Y = X2 * m_sin + Y * m_cos;
              }
              AddPoint(new IntPoint(
              Round(m_p[m_i][m_j].X + normals[m_j].X * m_delta),
              Round(m_p[m_i][m_j].Y + normals[m_j].Y * m_delta)));
          }
          //------------------------------------------------------------------------------

      } //end PolyOffsetBuilder
      //------------------------------------------------------------------------------

      internal static bool UpdateBotPt(IntPoint pt, ref IntPoint botPt)
      {
          if (pt.Y > botPt.Y || (pt.Y == botPt.Y && pt.X < botPt.X))
          {
              botPt = pt;
              return true;
          }
          else return false;
      }
      //------------------------------------------------------------------------------

      internal static bool StripDupsAndGetBotPt(Path in_path, 
        Path out_path, bool closed, out IntPoint botPt)
      {
        botPt = new IntPoint(0, 0);
        int len = in_path.Count;
        if (closed)    
          while (len > 0 && (in_path[0] == in_path[len -1])) len--;
        if (len == 0) return false;
        out_path.Capacity = len;
        int j = 0;
        out_path.Add(in_path[0]);
        botPt = in_path[0];
        for (int i = 1; i < len; ++i)
          if (in_path[i] != out_path[j])
          {
            out_path.Add(in_path[i]);
            j++;
            if (out_path[j].Y > botPt.Y ||
              ((out_path[j].Y == botPt.Y) && out_path[j].X < botPt.X))
                botPt = out_path[j];
          }
        j++;
        if (j < 2 || (closed && (j == 2))) j = 0;
        while (out_path.Count > j) out_path.RemoveAt(j);
        return j > 0;
      }
      //------------------------------------------------------------------------------

      public static Paths OffsetPaths(Paths polys, double delta,
          JoinType jointype, EndType endtype, double MiterLimit)
      {
        Paths out_polys = new Paths(polys.Count);
        IntPoint botPt = new IntPoint();
        IntPoint pt;
        int botIdx = -1;
        for (int i = 0; i < polys.Count; ++i)
        {
          out_polys.Add(new Path());
          if (StripDupsAndGetBotPt(polys[i], out_polys[i], endtype == EndType.etClosed, out pt))
            if (botIdx < 0 || pt.Y > botPt.Y || (pt.Y == botPt.Y && pt.X < botPt.X))
            {
              botPt = pt;
              botIdx = i;
            }
        }
        if (endtype == EndType.etClosed && botIdx >= 0 && !Orientation(out_polys[botIdx]))
          ReversePaths(out_polys);

        Paths result;
        new PolyOffsetBuilder(out_polys, out result, delta, jointype, endtype, MiterLimit);
        return result;
      }
      //------------------------------------------------------------------------------

#if use_deprecated
      public static Paths OffsetPolygons(Paths poly, double delta,
          JoinType jointype, double MiterLimit, bool AutoFix)
      {
        return OffsetPaths(poly, delta, jointype, EndType.etClosed, MiterLimit);
      }
      //------------------------------------------------------------------------------

      public static Paths OffsetPolygons(Paths poly, double delta,
          JoinType jointype, double MiterLimit)
      {
        return OffsetPaths(poly, delta, jointype, EndType.etClosed, MiterLimit);
      }
      //------------------------------------------------------------------------------

      public static Paths OffsetPolygons(Polygons polys, double delta, JoinType jointype)
      {
        return OffsetPaths(polys, delta, jointype, EndType.etClosed, 0);
      }
      //------------------------------------------------------------------------------

      public static Paths OffsetPolygons(Polygons polys, double delta)
      {
          return OffsetPolygons(polys, delta, JoinType.jtSquare, 0, true);
      }
      //------------------------------------------------------------------------------

      public static void ReversePolygons(Polygons polys)
      {
        polys.ForEach(delegate(Path poly) { poly.Reverse(); });
      }
      //------------------------------------------------------------------------------

      public static void PolyTreeToPolygons(PolyTree polytree, Polygons polys)
      {
        polys.Clear();
        polys.Capacity = polytree.Total;
        AddPolyNodeToPaths(polytree, NodeType.ntAny, polys);
      }
      //------------------------------------------------------------------------------
#endif

    //------------------------------------------------------------------------------
      // SimplifyPolygon functions ...
      // Convert self-intersecting polygons into simple polygons
      //------------------------------------------------------------------------------

      public static Paths SimplifyPolygon(Path poly, 
            PolyFillType fillType = PolyFillType.pftEvenOdd)
      {
          Paths result = new Paths();
          Clipper c = new Clipper();
          c.StrictlySimple = true;
          c.AddPath(poly, PolyType.ptSubject, true);
          c.Execute(ClipType.ctUnion, result, fillType, fillType);
          return result;
      }
      //------------------------------------------------------------------------------

      public static Paths SimplifyPolygons(Paths polys,
          PolyFillType fillType = PolyFillType.pftEvenOdd)
      {
          Paths result = new Paths();
          Clipper c = new Clipper();
          c.StrictlySimple = true;
          c.AddPaths(polys, PolyType.ptSubject, true);
          c.Execute(ClipType.ctUnion, result, fillType, fillType);
          return result;
      }
      //------------------------------------------------------------------------------

      private static double DistanceSqrd(IntPoint pt1, IntPoint pt2)
      {
        double dx = ((double)pt1.X - pt2.X);
        double dy = ((double)pt1.Y - pt2.Y);
        return (dx*dx + dy*dy);
      }
      //------------------------------------------------------------------------------

      private static DoublePoint ClosestPointOnLine(IntPoint pt, IntPoint linePt1, IntPoint linePt2)
      {
        double dx = ((double)linePt2.X - linePt1.X);
        double dy = ((double)linePt2.Y - linePt1.Y);
        if (dx == 0 && dy == 0) 
            return new DoublePoint(linePt1.X, linePt1.Y);
        double q = ((pt.X-linePt1.X)*dx + (pt.Y-linePt1.Y)*dy) / (dx*dx + dy*dy);
        return new DoublePoint(
            (1-q)*linePt1.X + q*linePt2.X, 
            (1-q)*linePt1.Y + q*linePt2.Y);
      }
      //------------------------------------------------------------------------------

      private static bool SlopesNearCollinear(IntPoint pt1, 
          IntPoint pt2, IntPoint pt3, double distSqrd)
      {
        if (DistanceSqrd(pt1, pt2) > DistanceSqrd(pt1, pt3)) return false;
        DoublePoint cpol = ClosestPointOnLine(pt2, pt1, pt3);
        double dx = pt2.X - cpol.X;
        double dy = pt2.Y - cpol.Y;
        return (dx*dx + dy*dy) < distSqrd;
      }
      //------------------------------------------------------------------------------

      private static bool PointsAreClose(IntPoint pt1, IntPoint pt2, double distSqrd)
      {
          double dx = (double)pt1.X - pt2.X;
          double dy = (double)pt1.Y - pt2.Y;
          return ((dx * dx) + (dy * dy) <= distSqrd);
      }
      //------------------------------------------------------------------------------

      public static Path CleanPolygon(Path path,
          double distance = 1.415)
      {
          //distance = proximity in units/pixels below which vertices
          //will be stripped. Default ~= sqrt(2) so when adjacent
          //vertices have both x & y coords within 1 unit, then
          //the second vertex will be stripped.
          double distSqrd = (distance * distance);
          int highI = path.Count -1;
          Path result = new Path(highI + 1);
          while (highI > 0 && PointsAreClose(path[highI], path[0], distSqrd)) highI--;
          if (highI < 2) return result;
          IntPoint pt = path[highI];
          int i = 0;
          for (;;)
          {
              while (i < highI && PointsAreClose(pt, path[i], distSqrd)) i+=2;
              int i2 = i;
              while (i < highI && (PointsAreClose(path[i], path[i + 1], distSqrd) ||
                  SlopesNearCollinear(pt, path[i], path[i + 1], distSqrd))) i++;
              if (i >= highI) break;
              else if (i != i2) continue;
              pt = path[i++];
              result.Add(pt);
          }
          if (i <= highI) result.Add(path[i]);
          i = result.Count;
          if (i > 2 && SlopesNearCollinear(result[i - 2], result[i - 1], result[0], distSqrd)) 
              result.RemoveAt(i -1);
          if (result.Count < 3) result.Clear();
          return result;
      }
      //------------------------------------------------------------------------------

      public static Paths CleanPolygons(Paths polys,
          double distance = 1.415)
      {
          Paths result = new Paths(polys.Count);
          for (int i = 0; i < polys.Count; i++)
              result.Add(CleanPolygon(polys[i], distance));
          return result;
      }
      //------------------------------------------------------------------------------

      internal enum NodeType { ntAny, ntOpen, ntClosed };

      public static Paths PolyTreeToPaths(PolyTree polytree)
      {

        Paths result = new Paths();
        result.Capacity = polytree.Total;
        AddPolyNodeToPaths(polytree, NodeType.ntAny, result);
        return result;
      }
      //------------------------------------------------------------------------------

      internal static void AddPolyNodeToPaths(PolyNode polynode, NodeType nt, Paths paths)
      {
        bool match = true;
        switch (nt)
        {
          case NodeType.ntOpen: return;
          case NodeType.ntClosed: match = !polynode.IsOpen; break;
          default: break;
        }

        if (polynode.Contour.Count > 0 && match) 
          paths.Add(polynode.Contour);
        foreach (PolyNode pn in polynode.Childs)
          AddPolyNodeToPaths(pn, nt, paths);
      }
      //------------------------------------------------------------------------------

      public static Paths OpenPathsFromPolyTree(PolyTree polytree)
      {
        Paths result = new Paths();
        result.Capacity = polytree.ChildCount;
        for (int i = 0; i < polytree.ChildCount; i++)
          if (polytree.Childs[i].IsOpen)
            result.Add(polytree.Childs[i].Contour);
        return result;
      }
      //------------------------------------------------------------------------------

      public static Paths ClosedPathsFromPolyTree(PolyTree polytree)
      {
        Paths result = new Paths();
        result.Capacity = polytree.Total;
        AddPolyNodeToPaths(polytree, NodeType.ntClosed, result);
        return result;
      }
      //------------------------------------------------------------------------------


  } //end ClipperLib namespace
  
  class ClipperException : Exception
  {
      public ClipperException(string description) : base(description){}
  }
  //------------------------------------------------------------------------------
}
