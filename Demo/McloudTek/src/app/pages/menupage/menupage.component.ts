import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderDetailsService } from 'src/app/services/order-details.service';

@Component({
  selector: 'app-menupage',
  templateUrl: './menupage.component.html',
  styleUrls: ['./menupage.component.css']
})
export class MenupageComponent implements OnInit{
constructor(private _param:ActivatedRoute,
  private _orderservice:OrderDetailsService
){}
getId:any;
menuId:any;
ngOnInit(): void {
  this.getId= this._param.snapshot.paramMap.get('id');
  console.log(this.getId,"getid");
  if(this.getId){
    this.menuId = this._orderservice.fooddetails.filter((value=>{
      return value.id == this.getId;
    }));
    console.log(this.menuId,"menuid");
    
  }
}
}
