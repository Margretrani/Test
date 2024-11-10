import { Component, OnInit } from '@angular/core';
import { OrderDetailsService } from 'src/app/services/order-details.service';

@Component({
  selector: 'app-menu',
  templateUrl: './menu.component.html',
  styleUrls: ['./menu.component.css']
})
export class MenuComponent implements OnInit{
foodData:any;
  constructor(private _orderservice:OrderDetailsService){

  }
  ngOnInit(): void {
    this.foodData=this._orderservice.fooddetails;
  }

}
