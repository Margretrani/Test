import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class OrderDetailsService {

  constructor() { }
  // fooddetails 
   fooddetails=[{
    id:1,
    foodName:"Panner Grilled sandwich",
    foodDetails:"Pan-fried masala panner",
    foodPrice:200,
    foodImg:"https://images.unsplash.com/photo-1690401767645-595de0e0e5f8?q=80&w=1426&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D"
   },
   {id:2,
    foodName:"Schewan fried rice",
    foodDetails:"fried rice",
    foodPrice:180,
    foodImg:"https://images.unsplash.com/photo-1626266799523-941311ea2273?q=80&w=1374&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D"
   },
   {id:3,
    foodName:"Mushroom gravy",
    foodDetails:"Fry with gravy",
    foodPrice:220,
    foodImg:"https://plus.unsplash.com/premium_photo-1673590981810-894dadc93a6d?q=80&w=1374&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D"
   },
   {id:4,
    foodName:"Egg rice",
    foodDetails:"Egg fried rice",
    foodPrice:150,
    foodImg:"https://images.unsplash.com/photo-1614019339893-573a8d44a768?q=80&w=1374&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D"
   },
   {id:5,
    foodName:"Butter chicken",
    foodDetails:"chicken-fried masala gravy",
    foodPrice:240,
    foodImg:"https://plus.unsplash.com/premium_photo-1676409608965-665e89ba22a4?q=80&w=1374&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D"
   },
  ]
}
