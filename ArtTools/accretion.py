"""Procedural lookup maps for the sky and interrupted CCTV transmission. Requires Pillow and NumPy."""
from pathlib import Path
import math
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1] / "Assets/Stillwater/Media/Optics"
ROOT.mkdir(parents=True, exist_ok=True)
RNG = np.random.default_rng(421819)


def periodic_field(w, h, nx, ny, seed):
    """Periodic smooth noise for use on a cylindrical stream surface."""
    grid = np.random.default_rng(seed).random((ny, nx))
    x, y = np.meshgrid(np.arange(w) * nx / w, np.arange(h) * ny / h)
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - ix, y - iy
    fx, fy = fx*fx*(3-2*fx), fy*fy*(3-2*fy)
    a = grid[iy%ny, ix%nx] * (1-fx) + grid[iy%ny, (ix+1)%nx]*fx
    b = grid[(iy+1)%ny, ix%nx] * (1-fx) + grid[(iy+1)%ny, (ix+1)%nx]*fx
    return a*(1-fy) + b*fy


def plasma():
    w,h=1024,512
    u,v=np.meshgrid(np.arange(w)/w,np.arange(h)/h)
    grain=periodic_field(w,h,23,37,11)
    knot=periodic_field(w,h,12,10,27)
    warp=.14*periodic_field(w,h,7,5,16)+.034*periodic_field(w,h,17,15,104)
    phi=2*np.pi*(v+warp+.021*np.sin(u*6*np.pi+v*8)+.008*np.sin(2*np.pi*(u*13+v*7)))
    ribbon=(.5+.5*np.cos(24*phi+grain*1.7))**3
    fine=(.5+.5*np.sin(57*phi+knot*3.5+grain*3.4))**6
    filaments=np.clip(.18+.49*ribbon+.30*fine+.21*grain,0,1)
    vortices=np.clip(.14+.60*knot+.26*periodic_field(w,h,43,24,252),0,1)
    clouds=periodic_field(w,h,48,89,140)
    Image.fromarray(np.uint8(np.clip(np.dstack((filaments,vortices,clouds)),0,1)*255)).save(ROOT/"Orbital streams.png",optimize=True)


def stone_relief():
    n=1024
    y,x=np.mgrid[:n,:n]/n
    tiles=x*6.+.5*(np.floor(y*12.)%2)
    dx=np.abs((tiles+.5)%1.-.5)*n/6.
    dy=np.abs((y*12.+.5)%1.-.5)*n/12.
    distance=np.minimum(dx,dy)
    mortar=np.exp(-(distance/1.75)**2)
    pebbles=periodic_field(n,n,208,207,71)
    low=periodic_field(n,n,33,35,101)
    h=(1-mortar)*1.65+.23*pebbles+.13*low
    nx=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*.4
    ny=(np.roll(h,-1,axis=0)-np.roll(h,1,axis=0))*.4
    rough=.19+.20*low+.10*pebbles+.44*mortar
    field=np.stack((.5-nx,.5+ny,rough),axis=2)
    Image.fromarray(np.uint8(np.clip(field,0,1)*255)).save(ROOT/"Pavement relief.png",optimize=True)


def star_chart():
    w,h=2048,1024
    u,v=np.meshgrid(np.arange(w)/w,np.arange(h)/h)
    web=periodic_field(w,h,10,5,224)
    fine=periodic_field(w,h,61,28,227)
    grain=periodic_field(w,h,134,69,50)
    latitude=(.5-v)*np.pi
    band=np.exp(-((latitude-.44*np.sin(u*2*np.pi+.7))/.19)**2)
    filament=band*np.maximum(0,web-.23)*(.19+.81*fine)*(.72+.28*grain)
    rgb=np.array([7.,11.,19.])+filament[:,:,None]*np.array([75.,89.,115.])
    rgb+=np.maximum(0,web-.42)[:,:,None]*np.array([6.,11.,18.])
    d=Image.fromarray(np.uint8(np.clip(rgb,0,255)))
    p=ImageDraw.Draw(d); rng=random.Random(4863)
    for _ in range(17500):
        x=rng.randrange(w);y=int((.5-math.asin(rng.uniform(-1,1))/np.pi)*h)%h
        s=rng.random()**5
        luminance=27+s*156
        tint=np.array(rng.choice([(1.,.85,.72),(.78,.90,1.),(.91,.97,1.)]))
        star=tuple(np.uint8(np.clip(np.array(d.getpixel((x,y)))+luminance*tint,0,255)))
        p.point((x,y),fill=star)
        if s>.993:p.line((x-1,y,x+1,y),fill=star)
    d.save(ROOT/"Deep field.jpg",quality=93,subsampling=0,optimize=True)


FILM_W,FILM_H=512,288
C=(286,83)
def make_sky():
    y,x=np.mgrid[:FILM_H,:FILM_W]
    r=np.hypot(x-C[0],y-C[1])
    a=np.arctan2(y-C[1],x-C[0])
    cloud=(periodic_field(FILM_W,FILM_H,6,5,714)-.5)*10
    bright=np.exp(-((y-169)/62.)**2)
    sky=np.stack((7+68*bright+cloud,13+61*bright+cloud,25+49*bright+cloud),axis=2)
    doppler=.82+.18*np.cos(a+.4)
    shift=(r-28)/28.
    cloudrim=np.exp(-np.maximum(0,shift)*5.)
    ribbon=(.5+.5*np.sin(43*shift+3*np.sin(3*a+shift*5)))**2
    fine=(.5+.5*np.cos(100*shift+2*np.sin(a*11+shift*1.7)))**5
    radiation=cloudrim*(.23+.47*ribbon+.26*fine)+np.exp(-abs(r-28)*.95)*1.2
    sky+=radiation[:,:,None]*np.stack((242*doppler,140*doppler,62*doppler),axis=2)
    sky[r<28]=0
    rng=random.Random(57)
    pic=Image.fromarray(np.uint8(np.clip(sky,0,255)))
    draw=ImageDraw.Draw(pic)
    for _ in range(270):
        px,py=rng.randrange(FILM_W),rng.randrange(140)
        if math.dist((px,py),C)>39:
            c=np.array(pic.getpixel((px,py)))+rng.randrange(10,82)*(1-py/160)
            draw.point((px,py),fill=tuple(np.uint8(c.clip(0,255))))
    return pic


class Tower:
    def __init__(self,x,ground,w,h,side,depth,seed):
        self.x,self.ground,self.w,self.h,self.side,self.depth=x,ground,w,h,side,depth
        rnd=random.Random(seed)
        pad=7
        sw=int(abs(side)); iw=int(w+sw+pad*2); ih=int(h+depth+pad*2+16)
        self.base=Image.new("RGBA",(iw,ih)); p=ImageDraw.Draw(self.base)
        self.offset=(pad+max(0,-side),ih-pad)
        x0,y0=self.offset
        x1=x0+w;roof=y0-h
        # Selectively lit office blinds and worn concrete panels. The sloped side
        # follows a perspective grid: every floor converges toward its rear face.
        front=np.array(rnd.choice([(52,58,62),(72,69,66),(47,62,67),(73,66,57),(40,48,55)]))
        flank=front*.59; trim=front*1.20
        def color(rgb):return tuple(np.uint8(np.clip(rgb,0,255)))
        p.rectangle((x0,roof,x1,y0),fill=color(front))
        sx=x1 if side>0 else x0
        p.polygon([(sx,roof),(sx+side,roof-depth),(sx+side,y0-depth),(sx,y0)],fill=color(flank))
        p.polygon([(x0,roof),(x1,roof),(x1+side,roof-depth),(x0+side,roof-depth)],fill=color(trim))
        p.line((x0,roof,x1,roof),fill=color(front*1.68))
        p.line((sx,roof,sx+side,roof-depth),fill=color(front*1.30))
        # Roof plants, antennas and water tanks give each tower an identifiable silhouette.
        for _ in range(rnd.randrange(1,4)):
            xx=x0+rnd.uniform(w*.10,w*.7); ww=max(2,w*rnd.uniform(.09,.24))
            hh=rnd.uniform(2,max(3,h*.055))
            yy=roof-depth*.5
            p.rectangle((xx,yy-hh,xx+ww,yy),fill=color(front*.63))
            p.line((xx,yy-hh,xx+ww,yy-hh),fill=color(trim),width=1)
        if rnd.random()<.28:
            xx=x0+w*rnd.uniform(.3,.7); p.line((xx,roof-2,xx,roof-h*.12-2),fill=(58,69,71))
            p.point((xx,roof-h*.12-2),fill=(235,94,45))
        cols=max(2,int(w/rnd.uniform(4.2,8.0)))
        rows=max(3,int(h/rnd.uniform(3.4,6.5)))
        pitch=w/(cols+.65); fh=(h-3)/(rows+.2)
        strip=rnd.random()<.45
        for row in range(rows):
            yy=roof+2+row*fh
            if strip or row%4==2:
                p.line((x0,yy+fh*.74,x1,yy+fh*.74),fill=color(front*.63))
            rowlit=rnd.random()<.10
            for col in range(cols):
                xx=x0+pitch*(.38+col);lit=rnd.random()<(.67 if rowlit else .23)
                win=rnd.choice([(224,157,82),(255,211,144),(120,163,173),(188,189,148)]) if lit else color(front*rnd.uniform(.28,.74))
                p.rectangle((xx,yy,xx+max(1,pitch*.61),yy+max(1,fh*.53)),fill=win)
                if pitch>6:p.line((xx+pitch*.31,yy,xx+pitch*.31,yy+fh*.53),fill=color(front*.57))
            for col in range(max(1,int(sw/6))):
                u=(col+.30)/max(1,int(sw/6))
                xx=sx+side*u; yp=yy-depth*u
                p.line((xx,yp,xx+side*.05,yp-depth*.05),fill=(161,145,108) if rnd.random()<.18 else color(flank*.5))
        for col in range(0,cols,3):
            xx=x0+(col+.18)*pitch
            p.line((xx,roof+1,xx,y0),fill=color(front*.72))
        self.twist=rnd.uniform(-77,75)
        self.start=rnd.uniform(.04,.38)
        self.orbit=rnd.choice([-1,1])*rnd.uniform(.42,1.00)
        self.lift=rnd.uniform(20,48)

    def paint(self, canvas, progress):
        if progress<=self.start:
            canvas.alpha_composite(self.base,(int(self.x-self.offset[0]),int(self.ground-self.offset[1])))
            return
        q=min(1,(progress-self.start)/(.96-self.start)); q=q*q*(3-2*q)
        if q>.985:return
        cx=self.x-self.offset[0]+self.base.width/2; cy=self.ground-self.offset[1]+self.base.height/2
        dx,dy=cx-C[0],cy-C[1]
        twist=self.orbit*(2.1*q+.4*q*q)
        cs,sn=math.cos(twist),math.sin(twist)
        cx=C[0]+(dx*cs-dy*sn)*(1-q);cy=C[1]+(dx*sn+dy*cs)*(1-q)-self.lift*math.sin(q*np.pi)
        scale=(1-q)**.85
        geo=self.base.resize((max(1,int(self.base.width*scale)),max(1,int(self.base.height*scale))),Image.Resampling.BICUBIC)
        geo=geo.rotate(self.twist*q+twist*45,Image.Resampling.BICUBIC,expand=True)
        canvas.alpha_composite(geo,(int(cx-geo.width/2),int(cy-geo.height/2)))


def flying_blocks():
    """Six original tower fragments for the last few degrees outside the shadow."""
    atlas=Image.new("RGBA",(768,128))
    for k in range(6):
        tower=Tower(0,0,45+(k*11)%24,57+(k*17)%44,12+k,5,817+k*31).base
        g=ImageDraw.Draw(tower)
        # Reveal an uneven cut foundation and a few reinforcing bars below it.
        for j in range(4):
            x=14+j*9
            g.line((x,tower.height-12,x+2,tower.height-2-j%3),fill=(103,81,68,255),width=2)
        tower.thumbnail((100,116),Image.Resampling.LANCZOS)
        atlas.alpha_composite(tower,(128*k+(128-tower.width)//2,(128-tower.height)//2))
    atlas.save(ROOT/"Orbital fragments.png",optimize=True)


def city_film():
    w,h=FILM_W,FILM_H;rnd=random.Random(2142)
    sky=make_sky();towers=[]
    ground=ImageDraw.Draw(sky)
    ground.rectangle((0,171,w,h),fill=(48,53,57))
    ground.rectangle((0,182,w,188),fill=(42,47,51))
    for xx in range(-80,w+60,33):
        ground.line((xx,171,260+(xx-260)*1.46,210),fill=(55,58,57))
    for yp in [176,182,191,205]:ground.line((0,yp,w,yp),fill=(40,46,48))
    # Back to front with a road ending just right of centre and varied height at the edges.
    for layer,(span,hmin,hmax,base,flank) in enumerate([(12,9,33,173,3),(19,15,48,181,5),(28,22,72,194,8),(43,43,107,210,15)]):
        x=-36
        while x<w+25:
            width=rnd.uniform(span*.67,span*1.6);mid=x+width/2
            gap=abs(mid-263.)/256
            if layer==3 and gap<.62:
                x+=width;continue
            height=rnd.uniform(hmin,hmax)*(.54+gap*.60)
            depth=rnd.uniform(1,5+layer); side=rnd.uniform(flank*.60,flank*1.70)*(1 if x<268 else -1)
            towers.append(Tower(x,base+rnd.uniform(-3,4),width,height,side,depth,rnd.randrange(1_000_000)))
            x+=width*rnd.uniform(.71,1.18)
    xpix=np.arange(w)[None,:];ypix=np.arange(h)[:,None]
    film=Image.new("RGB",(8*w,4*h)); count=32
    for k in range(count):
        progress=k/(count-1); c=sky.convert("RGBA")
        for tower in towers:tower.paint(c,progress)
        # Keep the event horizon absolutely dark wherever a foreground block disappears behind it.
        if progress>.02:
            d=ImageDraw.Draw(c);d.ellipse((C[0]-27,C[1]-27,C[0]+27,C[1]+27),fill=(0,0,0,255))
        img=np.asarray(c.convert("RGB")).astype(float)
        # Thin camera-side water from the empty square catches the passing architecture.
        street_y=210; y,x=np.mgrid[street_y:h,:w]
        refl_y=np.clip((street_y-(y-street_y)*1.40).astype(int),0,h-1)
        refl_x=np.clip((x+np.sin(y*1.73+k*.25)*1.6*np.sin(x*.027)).astype(int),0,w-1)
        reflected=img[refl_y,refl_x]*.39
        reflected+=np.array([7.,12.,15.])
        ridges=(np.sin(y*3.22+np.sin(x*.021+k*.2)*.32)*2+1)
        img[street_y:]=reflected+ridges[:,:,None]
        shade=1.-.19*((xpix-w*.5)/(w*.5))**2-.06*((ypix-h*.5)/(h*.5))**2
        noise=np.random.default_rng(45+k).normal(0,1.6,(h,w))
        pic=Image.fromarray(np.uint8(np.clip(img*shade[:,:,None]+noise[:,:,None],0,255)))
        p=ImageDraw.Draw(pic)
        # Stable lines in the wet foreground establish the distortion of the distant buildings.
        for xx in [-240,-50,92,422,540,710]:
            p.line([(int(264+(xx-264)*.29),street_y+1),(xx,h+2)],fill=(61,64,64),width=1)
        for yy in (219,234,256,281):
            p.line((0,yy,w,yy),fill=(34,40,42))
        film.paste(pic,((k%8)*w,(k//8)*h))
    film.save(ROOT/"Embankment signal.jpg",quality=91,subsampling=0,optimize=True)


def transmission_preview():
    """Portable recording of the receiver's full 24-second film and shutter."""
    sheet=Image.open(ROOT/"Embankment signal.jpg").convert("RGB")
    faces=[]
    # Decode the sRGB film before matching the PC receiver's linear-light dissolve.
    ramp=np.arange(256,dtype=np.float32)/255.
    linear=np.where(ramp<=.04045,ramp/12.92,((ramp+.055)/1.055)**2.4)
    for k in range(32):
        x=k%8*FILM_W; y=k//8*FILM_H
        faces.append(linear[np.asarray(sheet.crop((x,y,x+FILM_W,y+FILM_H)))])
    y,x=np.ogrid[-1:1:complex(FILM_H),-1:1:complex(FILM_W)]
    corners=(1.-.16*(x*x+y*y)).astype(np.float32)[:,:,None]*1.15

    def smooth(t):
        t=min(1.,max(0.,t))
        return t*t*(3.-2.*t)

    frames=[]; durations=[]; previous=None
    for tick in range(24*8):
        # Enter on a lit, intact view. The shutter and reopening bridge the loop.
        sec=(tick/8.+.5)%24.
        k=np.clip((sec-4.)/14.5,0.,1.)*31.
        mix=smooth((k%1.-.18)/.82)
        shutter=smooth(sec/.43)*(1.-smooth((sec-21.1)/.9))
        key=(int(k),mix,shutter)
        if key==previous:
            durations[-1]+=125
            continue
        img=faces[int(k)]*(1.-mix)+faces[min(31,int(k)+1)]*mix
        rgb=np.maximum(0.,img*corners*shutter)
        rgb=np.where(rgb<=.0031308,rgb*12.92,1.055*rgb**(1./2.4)-.055)
        frames.append(Image.fromarray(np.uint8(np.round(np.clip(rgb*255.,0,255)))))
        durations.append(125)
        previous=key
    output=Path(__file__).resolve().parents[1]/"Preview"/"Embankment transmission.webp"
    output.parent.mkdir(exist_ok=True)
    frames[0].save(output,save_all=True,append_images=frames[1:],duration=durations,
                   loop=0,quality=72,method=2,allow_mixed=True,kmin=1,kmax=1)


if __name__=="__main__":
    plasma()
    stone_relief()
    star_chart()
    flying_blocks()
    city_film()
    transmission_preview()
