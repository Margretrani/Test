import { Component, Input } from '@angular/core';
import { FooterDetails } from '@shared/service-proxies/service-proxies';

@Component({
  selector: 'app-footer-view',
  templateUrl: './footer-view.component.html',
  styleUrls: ['./footer-view.component.css']
})
export class FooterViewComponent {
  @Input()
  footerData: FooterDetails;  // Defines an input property
  ngOnInit() {
  }

  timeConvert(minutes) {
    if (minutes == 0)
      return "0 day(s) | 0:00 hour(s)"
    let days = Math.trunc(((minutes / 60) / 9 ) * 10) / 10;
    let hours = Math.floor((minutes / 60));
    let mins = Math.floor(minutes % 60);
    return days + ' day(s) | ' + hours + ':' + mins + ' hour(s)';
  }
}
