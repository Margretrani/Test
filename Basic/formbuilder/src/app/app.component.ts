import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent  implements OnInit{
 
  title = 'formbuilder';
  
    locationForm!: FormGroup ;
    countries: string[] = ['USA', 'Canada', 'India']; // Example countries
    states: { [key: string]: string[] } = {
      'USA': ['California', 'Texas', 'Florida'],
      'Canada': ['Ontario', 'Quebec', 'British Columbia'],
      'India': ['Maharashtra', 'Karnataka', 'Delhi']
    };
    cities: { [key: string]: string[] } = {
      'California': ['Los Angeles', 'San Francisco', 'San Diego'],
      'Texas': ['Houston', 'Dallas', 'Austin'],
      'Florida': ['Miami', 'Orlando', 'Tampa'],
      'Ontario': ['Toronto', 'Ottawa', 'Mississauga'],
      'Quebec': ['Montreal', 'Quebec City', 'Laval'],
      'British Columbia': ['Vancouver', 'Victoria', 'Burnaby'],
      'Maharashtra': ['Mumbai', 'Pune', 'Nagpur'],
      'Karnataka': ['Bengaluru', 'Mysuru', 'Mangalore'],
      'Delhi': ['New Delhi', 'Noida', 'Gurgaon']
    };
  
    filteredStates: string[] = [];
    filteredCities: string[] = [];
  
    constructor(private fb: FormBuilder) {}
  
    ngOnInit(): void {
      // Initialize form with default values
      this.locationForm = this.fb.group({
        country: [''],
        state: [''],
        city: ['']
      });
  
      // Watch country change to filter states
      this.locationForm.get('country')?.valueChanges.subscribe(country => {
        this.filteredStates = this.states[country] || [];
        this.locationForm.get('state')?.setValue('');
        this.filteredCities = []; // Reset cities
      });
  
      // Watch state change to filter cities
      this.locationForm.get('state')?.valueChanges.subscribe(state => {
        this.filteredCities = this.cities[state] || [];
        this.locationForm.get('city')?.setValue('');
      });
    }
  
   
  
  onSubmit(): void {
    console.log(this.locationForm.value);
  }

}