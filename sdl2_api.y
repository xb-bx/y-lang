let SDL_INIT_VIDEO: u32 = 0x00000020;
struct Window 
{
    fn destroy() 
    {
        SDL_DestroyWindow(this);
    }
}
struct Renderer 
{
    fn set_color(color: RGBA)
    {
        SDL_SetRenderDrawColor(this, color.r, color.g, color.b, color.a);
    }
    fn clear() 
    {
        SDL_RenderClear(this);
    }
    fn render_present()
    {
        SDL_RenderPresent(this);
    }
    fn fill_rect(rect: Rect)
    {
        SDL_RenderFillRect(this, &rect); 
    }
    fn draw_line(x: i32, y: i32, x1:i32, y1:i32)
    {
        SDL_RenderDrawLine(this, x, y, x1, y1);
    }
    fn destroy() 
    {
        SDL_DestroyRenderer(this);    
    }
}
enum EventType 
{
    Quit = 0x100,
    Display = 0x150,
    Window = 0x200,
    SysWM,
    
    KeyDown = 0x300,
    KeyUp,
    TextEditing,
    TextInput,
    KeyMapChanged,
    TextEditingExt,
    
    MouseMotion = 0x400,
    MouseButtonDown,
    MouseButtonUp,
    MouseWheel
}
struct __emptyShit 
{
    empty_shit: i64;
    empty_shit1: i64;
    empty_shit2: i64;
    empty_shit3: i64;
    empty_shit4: i64;
    empty_shit5: i64;
    empty_shit6: i64;
    empty_shit01: i64;
    empty_shit02: i64;
    empty_shit03: i64;
    empty_shit04: i64;
    empty_shit05: i64;
    empty_shit06: i64;
}
enum KeyCode 
{
    UNKNOWN = 0,
    A = 4,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    YY,
    Z,
    Num1,
    Num2,
    Num3,
    Num4,
    Num5,
    Num6,
    Num7,
    Num8,
    Num9
}
struct KeySym 
{
    scancode: KeyCode;
    sym: u32;
    mod: u32;
    __unused: u32;
}
struct KeyboardEvent 
{
    timestamp: u32;
    windowID: i32;
    state: i32;
    key: KeySym;
    
}
struct Event 
{
    type: EventType;
    union 
    {
        __shit: __emptyShit;
        keyboard: KeyboardEvent;
    }
}

struct RGBA 
{
    r: u8;
    g: u8;
    b: u8;
    a: u8;
    constructor(r: u8, g: u8, b: u8, a: u8)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

}
struct Rect
{
    x: i32;
    y: i32;
    w: i32;
    h: i32;
    constructor(x: i32, y: i32, w: i32, h: i32)
    {
        this.x = x;
        this.y = y;
        this.w = w;
        this.h = h;
    }

}
dllimport "SDL2.dll" 
{
    extern fn SDL_Init(flags: u32): i32;
    extern fn SDL_CreateWindow(title: *char, x: i32, y: i32, w: i32, h: i32, flags: u32): *Window;
    extern fn SDL_CreateRenderer(window: *Window, id: i32, flags: u32): *Renderer;
    extern fn SDL_GetError(): *char;
    extern fn SDL_WaitEvent(event: *Event): i32;
    extern fn SDL_PollEvent(event: *Event): i32;
    extern fn SDL_SetRenderDrawColor(renderer: *Renderer, r: u8, g: u8, b: u8, a: u8): i32;
    extern fn SDL_RenderClear(renderer: *Renderer): i32;
    extern fn SDL_RenderPresent(renderer: *Renderer);
    extern fn SDL_RenderFillRect(renderer: *Renderer, rect: *Rect);
    extern fn SDL_RenderDrawLine(renderer: *Renderer, x1: i32, y1: i32, x2:i32, y:i32);
    extern fn SDL_DestroyWindow(window: *Window);
    extern fn SDL_DestroyRenderer(renderer: *Renderer);
    extern fn SDL_Delay(ms: i32); 
    extern fn SDL_Quit();
}
