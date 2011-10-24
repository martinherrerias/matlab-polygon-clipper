#include <windows.h>
#include <commctrl.h>
#include <gl/gl.h>
#include <gl/glu.h>
#include <ctime>
#include <cmath>
#include <sstream>
#include <fstream>
#include <iomanip>
#include "../clipper.hpp"

using namespace std;
using namespace ClipperLib;

enum poly_color_type { pctSubject, pctClip, pctSolution };

//global vars ...
HWND		 hWnd;
HWND     hStatus; 
HDC			 hDC;
HGLRC		 hRC;
ClipType     ct = ctIntersection;
PolyFillType pft = pftEvenOdd;
bool show_clipping = true;
Polygons sub, clp, sol;
int VertCount = 5;

typedef std::vector< GLdouble* > Vectors;
Vectors vectors;

#define array_len(a) ( sizeof ( a ) / sizeof ( *a ) )

//------------------------------------------------------------------------------
// heap memory management for GLUtesselator ...
//------------------------------------------------------------------------------

GLdouble* NewVector(GLdouble x, GLdouble y)
{
  GLdouble *vert = new GLdouble[3];
  vert[0] = x;
  vert[1] = y;
  vert[2] = 0;
  vectors.push_back(vert);
  return vert;
}
//------------------------------------------------------------------------------

void ClearVectors()
{
  for (Vectors::size_type i = 0; i < vectors.size(); ++i)
    delete[] vectors[i];
  vectors.clear(); 
}

//------------------------------------------------------------------------------
// GLUtesselator callback functions ...
//------------------------------------------------------------------------------

void CALLBACK BeginCallback(GLenum type)   
{   
    glBegin(type);   
} 
//------------------------------------------------------------------------------

void CALLBACK EndCallback()   
{   
    glEnd();   
}
//------------------------------------------------------------------------------

void CALLBACK VertexCallback(GLvoid *vertex)   
{   
	glVertex3dv( (const double *)vertex );   
} 
//------------------------------------------------------------------------------

void CALLBACK CombineCallback(GLdouble coords[3], 
  GLdouble *data[4], GLfloat weight[4], GLdouble **dataOut )   
{   
  GLdouble *vert = NewVector(coords[0], coords[1]);
	*dataOut = vert;
}   
//------------------------------------------------------------------------------

wstring str2wstr(const std::string &s) {
	int slength = (int)s.length() + 1;
	int len = MultiByteToWideChar(CP_ACP, 0, s.c_str(), slength, 0, 0); 
	wchar_t* buf = new wchar_t[len];
  MultiByteToWideChar(CP_ACP, 0, s.c_str(), slength, buf, len);
  std::wstring r(buf);
  delete[] buf;
  return r;
}
//------------------------------------------------------------------------------

void CALLBACK ErrorCallback(GLenum errorCode)   
{   
	std::wstring s = str2wstr( (char *)gluErrorString(errorCode) );
	SetWindowText(hWnd, s.c_str());
}   

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

// Set up pixel format for graphics initialization
void SetupPixelFormat()
{
    PIXELFORMATDESCRIPTOR pfd, *ppfd;
    int pixelformat;

    ppfd = &pfd;

    ppfd->nSize = sizeof(PIXELFORMATDESCRIPTOR);
    ppfd->nVersion = 1;
    ppfd->dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER;
    ppfd->dwLayerMask = PFD_MAIN_PLANE;
    ppfd->iPixelType = PFD_TYPE_RGBA;
    ppfd->cColorBits = 16;
    ppfd->cDepthBits = 16;
    ppfd->cAccumBits = 0;
    ppfd->cStencilBits = 0;

    pixelformat = ChoosePixelFormat(hDC, ppfd);
    SetPixelFormat(hDC, pixelformat, ppfd);
}
//------------------------------------------------------------------------------

// Initialize OpenGL graphics
void InitGraphics()
{
  hDC = GetDC(hWnd);
  SetupPixelFormat();
  hRC = wglCreateContext(hDC);
  wglMakeCurrent(hDC, hRC);
	glDisable(GL_DEPTH_TEST);
	glEnable(GL_BLEND);
	glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
  glTranslatef (0.375, 0.375, 0);
}
//------------------------------------------------------------------------------

void MakeRandomPoly(ClipperLib::Polygon &p, int width, int height, int edgeCount)
{
	p.resize(edgeCount);
	for (int i = 0; i < edgeCount; i++)
	{
		p[i].X = rand()%(width-20) +10;
		p[i].Y = rand()%(height-20) +10;
	}
}
//------------------------------------------------------------------------------

void ResizeGraphics(int width, int height)
{
  //setup 2D projection with origin at top-left corner ...
	glMatrixMode(GL_PROJECTION);
	glLoadIdentity();
	glOrtho(0, width, height, 0, 0, 1);
	glViewport(0, 0, width, height);
	glMatrixMode(GL_MODELVIEW);
	glLoadIdentity();
}
//------------------------------------------------------------------------------

void DrawPolygon(Polygons &pgs, poly_color_type pct)
{
	switch (pct)
	{
		case pctSubject: glColor4f(0.0f, 0.0f, 1.0f, 0.062f); break;
		case pctClip: glColor4f(1.0f, 1.0f, 0.0f, 0.062f); break;
		default: glColor4f(0.0f, 1.0f, 0.0f, 0.25f);
	}

	GLUtesselator* tess = gluNewTess();
  gluTessCallback(tess, GLU_TESS_BEGIN, (void (CALLBACK*)())&BeginCallback);    
  gluTessCallback(tess, GLU_TESS_VERTEX, (void (CALLBACK*)())&VertexCallback);    
  gluTessCallback(tess, GLU_TESS_END, (void (CALLBACK*)())&EndCallback);   
  gluTessCallback(tess, GLU_TESS_COMBINE, (void (CALLBACK*)())&CombineCallback);   
  gluTessCallback(tess, GLU_TESS_ERROR, (void (CALLBACK*)())&ErrorCallback);
  gluTessNormal(tess, 0.0, 0.0, 1.0);
	
	switch (pft)
  {
    case pftEvenOdd: 
      gluTessProperty(tess, GLU_TESS_WINDING_RULE, GLU_TESS_WINDING_ODD); 
      break;
    case pftNonZero: 
      gluTessProperty(tess, GLU_TESS_WINDING_RULE, GLU_TESS_WINDING_NONZERO); 
      break;
    case pftPositive: 
      gluTessProperty(tess, GLU_TESS_WINDING_RULE, GLU_TESS_WINDING_POSITIVE); 
      break;
    default: 
      if (pct == pctSolution)
        gluTessProperty(tess, GLU_TESS_WINDING_RULE, GLU_TESS_WINDING_NONZERO);
      else
        gluTessProperty(tess, GLU_TESS_WINDING_RULE, GLU_TESS_WINDING_NEGATIVE);
  }

	gluTessProperty(tess, GLU_TESS_BOUNDARY_ONLY, GL_FALSE); //GL_FALSE
	gluTessBeginPolygon(tess, NULL); 
	for (Polygons::size_type i = 0; i < pgs.size(); ++i)
	{
		gluTessBeginContour(tess);
		for (ClipperLib::Polygon::size_type j = 0; j < pgs[i].size(); ++j)
		{
      GLdouble *vert = 
        NewVector((GLdouble)pgs[i][j].X, (GLdouble)pgs[i][j].Y);
			gluTessVertex(tess, vert, vert); 
		}
		gluTessEndContour(tess); 
	}
	gluTessEndPolygon(tess);
  ClearVectors();

	switch (pct)
	{
		case pctSubject: 
      glColor4f(0.0f, 0.6f, 1.0f, 0.5f); 
      break;
		case pctClip: 
      glColor4f(1.0f, 0.6f, 0.0f, 0.5f); 
      break;
		default: 
      glColor4f(0.0f, 0.4f, 0.0f, 1.0f);
	}
	if (pct == pctSolution) glLineWidth(1.0f); else glLineWidth(0.8f);

  gluTessProperty(tess, GLU_TESS_WINDING_RULE, GLU_TESS_WINDING_ODD); 
	gluTessProperty(tess, GLU_TESS_BOUNDARY_ONLY, GL_TRUE);
	for (Polygons::size_type i = 0; i < pgs.size(); ++i)
	{
    gluTessBeginPolygon(tess, NULL); 
		gluTessBeginContour(tess);
		for (ClipperLib::Polygon::size_type j = 0; j < pgs[i].size(); ++j)
		{
			GLdouble *vert = 
        NewVector((GLdouble)pgs[i][j].X, (GLdouble)pgs[i][j].Y);
			gluTessVertex(tess, vert, vert); 
		}

    switch (pct)
	  {
		  case pctSubject: 
        if (Orientation(pgs[i])) 
          glColor4f(0.0f, 0.0f, 0.8f, 0.5f); 
        else
          glColor4f(0.0f, 1.0f, 1.0f, 0.5f); 
        break;
		  case pctClip: 
        if (Orientation(pgs[i])) 
          glColor4f(0.6f, 0.0f, 0.0f, 0.5f); 
        else
          glColor4f(1.0f, 0.25f, 1.0f, 0.5f); 
	  }
		gluTessEndContour(tess);
	  gluTessEndPolygon(tess);
	}

	//final cleanup ...
	gluDeleteTess(tess);
  ClearVectors();
}
//------------------------------------------------------------------------------

void DrawGraphics()
{
	//this can take a few moments ...
	HCURSOR hWaitCursor = LoadCursor(NULL, IDC_WAIT);
	SetCursor(hWaitCursor);
	SetClassLong(hWnd, GCL_HCURSOR, (DWORD)hWaitCursor);

	//fill background with a light off-gray color ...
	glClearColor(1,1,1,1);
	glClear(GL_COLOR_BUFFER_BIT);

	DrawPolygon(sub, pctSubject);
	DrawPolygon(clp, pctClip);
  if (show_clipping) DrawPolygon(sol, pctSolution);
	
  wstringstream ss;
  if (!show_clipping)
    ss << L"Clipper Demo - No clipping"; 
  else
	  switch (ct)
	  {
		  case ctUnion: 
        ss << L"Clipper Demo - Union"; 
        break;
		  case ctDifference: 
        ss << L"Clipper Demo - Difference"; 
        break;
		  case ctXor: 
        ss << L"Clipper Demo - XOR"; 
        break;
		  default: 
        ss << L"Clipper Demo - Intersection"; 
	  }

	switch(pft)
  {
    case pftEvenOdd: 
      ss << L" with EvenOdd fill"; 
      break;
    case pftNonZero: 
      ss << L" with NonZero fill"; 
      break;
    case pftPositive: 
      ss << L" with Positive fill"; 
      break;
    default: 
      ss << L" with Negative fill"; 
  }
  ss << L".  [vertex count = " << VertCount << "]";
	SetWindowText(hWnd, ss.str().c_str());

	HCURSOR hArrowCursor = LoadCursor(NULL, IDC_ARROW);
	SetCursor(hArrowCursor);
	SetClassLong(hWnd, GCL_HCURSOR, (DWORD)hArrowCursor);
}
//------------------------------------------------------------------------------

inline long64 Round(double val)
{
  if ((val < 0)) return (long64)(val - 0.5); else return (long64)(val + 0.5);
}
//------------------------------------------------------------------------------

//bool LoadFromFile(Polygons &ppg, char * filename, double scale= 1,
//  int xOffset = 0, int yOffset = 0)
//{
//  ppg.clear();
//  ifstream infile(filename);
//  if (!infile.is_open()) return false;
//  int polyCnt, vertCnt;
//  double X, Y;
//  
//  infile >> polyCnt;
//  infile.ignore(80, '\n');
//  if (infile.good() && polyCnt > 0)
//  {
//    ppg.resize(polyCnt);
//    for (int i = 0; i < polyCnt; i++) 
//    {
//      infile >> vertCnt;
//      infile.ignore(80, '\n');
//      if (!infile.good() || vertCnt < 0) break;
//      ppg[i].resize(vertCnt);
//      for (int j = 0; infile.good() && j < vertCnt; j++) 
//      {
//        infile >> X;
//        while (infile.peek() == ' ') infile.ignore();
//        if (infile.peek() == ',') infile.ignore();
//        while (infile.peek() == ' ') infile.ignore();
//        infile >> Y;
//        ppg[i][j].X = Round((X + xOffset) * scale);
//        ppg[i][j].Y = Round((Y + yOffset) * scale);
//        infile.ignore(80, '\n');
//      }
//    }
//  }
//  infile.close();
//  return true;
//}
//------------------------------------------------------------------------------

void SaveToFile(const char *filename, Polygons &pp, double scale = 1)
{
  ofstream of(filename);
  if (!of.is_open()) return;
  of << pp.size() << "\n";
  for (Polygons::size_type i = 0; i < pp.size(); ++i)
  {
    of << pp[i].size() << "\n";
    if (scale > 1.01 || scale < 0.99) 
      of << fixed << setprecision(6);
    for (Polygon::size_type j = 0; j < pp[i].size(); ++j)
      of << (double)pp[i][j].X /scale << ", " << (double)pp[i][j].Y /scale << ",\n";
  }
  of.close();
}
//---------------------------------------------------------------------------

//void MakePolygonFromInts(int *ints, int size, ClipperLib::Polygon &p)
//{
//  p.clear();
//  p.reserve(size / 2);
//  for (int i = 0; i < size; i +=2)
//    p.push_back(IntPoint(ints[i], ints[i+1]));
//}
//---------------------------------------------------------------------------

//void MakePolygonFromIntArray(const int ints[][2], int size, ClipperLib::Polygon &p)
//{
//  p.clear();
//  p.reserve(size / 2);
//  for (int i = 0; i < size; ++i)
//    p.push_back(IntPoint(ints[i][0], ints[i][1]));
//}
//---------------------------------------------------------------------------

void UpdatePolygons(bool updateSolutionOnly)
{
	if (VertCount < 0) VertCount = -VertCount;
  if (VertCount > 50) VertCount = 50;
  if (VertCount < 3) VertCount = 3;

  Clipper c;
	if (!updateSolutionOnly)
	{
    RECT r;
    GetWindowRect(hStatus, &r);
    int statusHeight = r.bottom - r.top;
    GetClientRect(hWnd, &r);

    sub.resize(1);
    clp.resize(1);

    MakeRandomPoly(sub[0], r.right, r.bottom - statusHeight, VertCount);
    MakeRandomPoly(clp[0], r.right, r.bottom - statusHeight, VertCount);
    SaveToFile("subj.txt", sub);
    SaveToFile("clip.txt", clp);

    //LoadFromFile(sub, "subj.txt");
    //LoadFromFile(clp, "clip.txt");

	}
	c.AddPolygons(sub, ptSubject);
	c.AddPolygons(clp, ptClip);
	c.Execute(ct, sol, pft, pft);

	InvalidateRect(hWnd, NULL, false); 
}
//------------------------------------------------------------------------------

void DoNumericKeyPress(int  numChr)
{
  if (VertCount >= 0) VertCount = -(numChr - '0');
  else if (VertCount > -10) VertCount = VertCount*10 - (numChr - '0');
  else Beep(1000, 100);
}
//------------------------------------------------------------------------------

LONG WINAPI MainWndProc (HWND hWnd, UINT uMsg, WPARAM  wParam, LPARAM  lParam)
{
	int clientwidth, clientheight;
    switch (uMsg)
    {

	case WM_SIZE:
		clientwidth = LOWORD(lParam);
		clientheight = HIWORD(lParam);
		ResizeGraphics(clientwidth, clientheight);
		SetWindowPos(hStatus, NULL, 0, 
      clientheight, clientwidth, 0, SWP_NOACTIVATE | SWP_NOZORDER);
        return 0;

	case WM_PAINT:
		HDC hdc;
		PAINTSTRUCT ps;
		hdc = BeginPaint(hWnd, &ps);
		//do the drawing ...
		DrawGraphics();
		SwapBuffers(hdc);
		EndPaint(hWnd, &ps);		
		return 0;

    case WM_CLOSE: 
        DestroyWindow(hWnd);
        return 0;
 
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;

	case WM_HELP:
        MessageBox(hWnd, 
			L"Clipper Demo tips...\n\n"
			L"I - for Intersection operations.\n"
			L"U - for Union operations.\n" 
			L"D - for Difference operations.\n"
			L"X - for XOR operations.\n" 
			L"Q - for no clipping.\n" 
			L"------------------------------\n" 
			L"E - for EvenOdd fills.\n" 
			L"Z - for NonZero fills.\n" 
			L"P - for Positive fills.\n" 
			L"N - for Negative fills.\n" 
			L"------------------------------\n" 
			L"nn<ENTER> - number of vertices (3..50).\n" 
			L"------------------------------\n" 
			L"SPACE, ENTER or click to refresh.\n" 
			L"F1 - to see this help dialog again.\n"
			L"Esc - to quit.\n",
			L"Clipper Demo - Help", 0);
        return 0;

    case WM_CHAR:
		switch (wParam)
		{
			case VK_ESCAPE: PostQuitMessage(0); break; 
			case 'e': 
			case 'E': pft = pftEvenOdd; UpdatePolygons(true); break;
			case 'z': 
			case 'Z': pft = pftNonZero; UpdatePolygons(true); break;
			case 'p': 
      case 'P': pft = pftPositive; UpdatePolygons(true); break;
			case 'n': 
      case 'N': pft = pftNegative; UpdatePolygons(true); break;
			case 'i': 
			case 'I': show_clipping = true; ct = ctIntersection; UpdatePolygons(true); break;
			case 'd': 
			case 'D': show_clipping = true; ct = ctDifference; UpdatePolygons(true); break;
			case 'u': 
			case 'U': show_clipping = true; ct = ctUnion; UpdatePolygons(true); break;
			case 'x': 
			case 'X': show_clipping = true; ct = ctXor; UpdatePolygons(true); break;
			case 'q': 
			case 'Q': show_clipping = false; UpdatePolygons(true); break;
      case '0': case '1': case '2': case '3': case '4':
      case '5': case '6': case '7': case '8': case '9': 
        DoNumericKeyPress((int)wParam); break;
			case VK_SPACE:
			case VK_RETURN: UpdatePolygons(false);
		}
        return 0;

	case WM_LBUTTONUP:
		UpdatePolygons(false);
		return 0;

	// Default event handler
    default: return DefWindowProc (hWnd, uMsg, wParam, lParam); 
    }  
}
//------------------------------------------------------------------------------

int WINAPI WinMain (HINSTANCE hInstance, 
  HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{

    const LPCWSTR appname = TEXT("Clipper Demo");

    WNDCLASS wndclass;
    MSG      msg;
 
    // Define the window class
    wndclass.style         = 0;
    wndclass.lpfnWndProc   = (WNDPROC)MainWndProc;
    wndclass.cbClsExtra    = 0;
    wndclass.cbWndExtra    = 0;
    wndclass.hInstance     = hInstance;
    wndclass.hIcon         = LoadIcon(hInstance, appname);
    wndclass.hCursor       = LoadCursor(NULL, IDC_ARROW);
    wndclass.hbrBackground = (HBRUSH)(COLOR_WINDOW+1);
    wndclass.lpszMenuName  = appname;
    wndclass.lpszClassName = appname;
 
    // Register the window class
    if (!RegisterClass(&wndclass)) return FALSE;
 
    // Create the window
    hWnd = CreateWindow(
            appname,
            appname,
            WS_OVERLAPPEDWINDOW | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            640,
            480,
            NULL,
            NULL,
            hInstance,
            NULL);
 
    if (!hWnd) return FALSE;

	//replace the main window icon with Resource Icon #1 ...
  HANDLE small_ico = LoadImage(hInstance, MAKEINTRESOURCE(1), IMAGE_ICON, 16, 16, 0);
	HANDLE big_ico = LoadImage(hInstance, MAKEINTRESOURCE(1), IMAGE_ICON, 32, 32, 0);
	SendMessage(hWnd, WM_SETICON, ICON_SMALL, (LPARAM)small_ico);
	SendMessage(hWnd, WM_SETICON, ICON_BIG, (LPARAM)big_ico);

	InitCommonControls();
	hStatus = CreateWindowEx(0, L"msctls_statusbar32", NULL, WS_CHILD | WS_VISIBLE,
		0, 0, 0, 0, hWnd, (HMENU)0, hInstance, NULL);
	SetWindowText(hStatus, L"  F1 for help");

  // Initialize OpenGL
  InitGraphics();

	srand((unsigned)time(0)); 
	UpdatePolygons(false);

  // Display the window
  ShowWindow(hWnd, nCmdShow);
  UpdateWindow(hWnd);

  // Event loop
    while (true)
    {
        if (PeekMessage(&msg, NULL, 0, 0, PM_NOREMOVE) == TRUE)
        {
            if (!GetMessage(&msg, NULL, 0, 0)) return TRUE;

            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }
	wglMakeCurrent(NULL, NULL);
    wglDeleteContext(hRC);
    ReleaseDC(hWnd, hDC);
}
//------------------------------------------------------------------------------
