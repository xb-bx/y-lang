include "sdl2_api.y";
let CELL_SIZE = 20;
let window_width = 640;
let window_height = 480;
let dir = new Point(1, 0);
let snake: *Point = null;
let snakelen: u64 = 0;
let mapsize = new Point(0, 0);
let seed = 0xff;
let fruit = new Point(0, 0);
struct Point 
{
    x: i32;
    y: i32;
    constructor(x: i32, y: i32) 
    {
        this.x = x;
        this.y = y;
    }
}
fn main()
{
    seed = GetTickCount();
    snake = cast(alloc(2048), *Point);
    snakelen = 2;
    snake[0] = new Point(1, 1);
    snake[1] = new Point(1, 1);
    mapsize = new Point(window_width / CELL_SIZE, window_height / CELL_SIZE);
    fruit.x = nextrnd() % mapsize.x;
    fruit.y = nextrnd() % mapsize.y;
    let event = cast(alloc(512), *Event);
    if SDL_Init(SDL_INIT_VIDEO) < 0 
    {
        writestr(SDL_GetError());
        ret;
    }
    let window = SDL_CreateWindow("w", 100, 100, window_width, window_height, 0);
    if window == null
    {
        writestr(SDL_GetError());
        ret;
    }
    let renderer = SDL_CreateRenderer(window, -1, 0);
    let count = 0;
    let isQuit = false;
    while isQuit == false 
    {
        SDL_PollEvent(event);
        if event.type == EventType.Quit isQuit = true;
        if isQuit == false 
        {
            if event.type == EventType.KeyDown 
            {
                change_dir(event);
            }
            if count == 0 
            {
                movesnake();
                count = 50;
            }
            renderer.set_color(new RGBA(0, 0, 0, 0));
            renderer.clear();
            draw_grid(renderer);
            draw_snake(renderer); 
            draw_fruit(renderer);
            renderer.render_present();
        }
        count = count - 1;
    }
    renderer.destroy();
    window.destroy();
    SDL_Quit();
}

fn draw_snake(renderer: *Renderer) 
{
    let i: u64 = 0;
    let size = CELL_SIZE - 1;
    renderer.set_color(new RGBA(0xff, 0xff,0,0xff));
    while i < snakelen 
    {
        let pos = snake[i];
        renderer.fill_rect(new Rect(pos.x * CELL_SIZE + 1, pos.y * CELL_SIZE + 1, size, size));
        i = i + 1;
    }
}
fn movesnake()
{
    let i = snakelen - 1;
    while i > 0 
    {
        snake[i] = snake[i - 1];
        i = i - 1;

    }
    let head = snake[0];
    head.x = head.x + dir.x;
    head.y = head.y + dir.y;
    if head.x >= mapsize.x head.x = 0;
    else if head.x < 0 head.x = mapsize.x - 1;
    if head.y >= mapsize.y head.y = 0;
    else if head.y < 0 head.y = mapsize.y - 1;
    snake[0] = head;
    i = 1;
    while i < snakelen 
    {
        if head.x == snake[i].x && head.y == snake[i].y 
        {
            snakelen = i;
            ret;
        }    
        i = i + 1;
    }
    if head.x == fruit.x && head.y == fruit.y 
    {
        fruit.x = nextrnd() % mapsize.x;
        fruit.y = nextrnd() % mapsize.y;
        snake[snakelen] = snake[snakelen - 1];
        snakelen = snakelen + 1;

    }
}
fn change_dir(event: *Event)
{
    let keycode = event.keyboard.key.scancode;
    if keycode == KeyCode.A && dir.x != 1 dir = new Point(-1, 0);
    else if keycode == KeyCode.D && dir.x != -1 dir = new Point(1, 0);
    else if keycode == KeyCode.W && dir.y != 1 dir = new Point(0, -1);
    else if keycode == KeyCode.S && dir.y != -1 dir = new Point(0, 1);
}
fn nextrnd() : i32
{
    let rnd = 0;
    asm 
    {
        mov edx, 0
        mov ecx, 1103515245
        mov eax, dword[seed]
        mul ecx 
        add eax, 12345
        mov dword[seed], eax
        mov edx, 0
        mov ecx, 65536 
        div ecx
        mov edx, 0
        mov ecx, 32768
        div ecx
        mov dword[rbp - 8], edx
    }
    ret rnd;
}
fn draw_grid(renderer: *Renderer)
{
    let i = 0;
    let count = window_height / CELL_SIZE;
    renderer.set_color(new RGBA(0, 0xaa, 0,0xaa));
    while i < count 
    {
        renderer.draw_line(0, i * CELL_SIZE, window_width, i * CELL_SIZE);
        i = i + 1;
    }
    i = 0;
    count = window_width / CELL_SIZE;
    while i < count 
    {
        renderer.draw_line(i * CELL_SIZE, 0, i * CELL_SIZE, window_height);
        i = i + 1;
    }
}
fn draw_fruit(renderer: *Renderer) 
{
    renderer.set_color(new RGBA(255, 128, 0, 255));
    renderer.fill_rect(new Rect(fruit.x * CELL_SIZE + 1, fruit.y * CELL_SIZE + 1, CELL_SIZE - 1, CELL_SIZE - 1));
}
fn quit() 
{
    SDL_Quit();
}
