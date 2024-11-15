import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { SharedService } from '@shared/services/shared.service';
import { DateTime } from 'luxon';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-canvas-display',
  templateUrl: './canvas-view.component.html'
})
export class Canvas implements OnInit {
  @Input() newRec: any;
  @Output() onclose = new EventEmitter();
  @Output() closeEvent = new EventEmitter<void>(); 
  hours: string = '';
  sortedTimes: DateTime[] = [];
  pairs: { checkIn: DateTime; checkOut: DateTime }[] = [];
  record: any;
  private subscription: Subscription;

  constructor(private sharedService: SharedService) {}

  ngOnInit(): void {
    this.subscription = this.sharedService.value$.subscribe(value => {
      this.record = value;
      if (this.record) {
        this.sortCheckInOutTimes();
        if (this.record.permissionHours) {
          this.hours = this.convertMinutesToHours(this.record.permissionHours);
        }
        if (this.record.status === 'Leave' || this.record.status === 'LOP') {
        }
      }
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }

  closeOffcanvas(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (target.classList.contains('offcanvas-end')) {
      this.close();
    }
  }

  close(): void {
    this.closeEvent.emit(); 
    const closeDate = DateTime.now(); 
    this.onclose.emit(closeDate); 
    this.clearData(); 
  }

  private clearData(): void {
    this.record = null; 
    this.sortedTimes = []; 
    this.pairs = [];
  }

  sortCheckInOutTimes(): void {
    if (!this.record) return;
    console.log(this.record,"record");
    
    const uniqueTimesSet = new Set<string>();
    console.log(uniqueTimesSet,"filterrecord");
    
    this.sortedTimes = [...this.record.allcheckIn,...this.record.allcheckOut].filter(time => {
      const timeString = time.toISO(); 
      if (uniqueTimesSet.has(timeString)) {
        return false; 
      } else {
        uniqueTimesSet.add(timeString); 
        return true;
      }
    });
    console.log(this.record.allcheckIn,"checkins");
    console.log(this.record.allcheckOut,"checkouts");
    
    
    console.log(this.sortedTimes,"sorttimes");
    
    // this.sortedTimes.sort((a: DateTime, b: DateTime) => a.toMillis() - b.toMillis());
    this.pairCheckInOutTimes();
  }

  pairCheckInOutTimes(): void {

    this.pairs = []; 
    console.log(this.pairs,"pairs");
    
    for (let i = 0; i < this.record.allcheckIn.length || i< this.record.allcheckOut.length; i ++) {
      // if (i + 1 < this.sortedTimes.length) {
      //   this.pairs.push({ checkIn: this.sortedTimes[i], checkOut: this.sortedTimes[i + 1] });
      // } else {
      //   this.pairs.push({ checkIn: this.sortedTimes[i], checkOut: null });
      // }
      this.pairs.push({ checkIn: this.record.allcheckIn[i], checkOut: this.record.allcheckOut[i] });
    }
  }

  convertMinutesToHours(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins.toString().padStart(2, '0')}m`;
  }
}
