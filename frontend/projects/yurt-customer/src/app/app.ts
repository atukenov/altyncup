import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { ToastContainerComponent } from 'shared-ui';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  private api = inject(YurtApiService);

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
  }
}
