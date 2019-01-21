import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';
import { AtomicCounterClient, getAuthToken } from './atomic-counter/build/dist/atomicCounter';
import GoogleLogin from './GoogleLogin';
import Tenant from './Tenant';

import logo from './logo.svg';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

interface IAppState {
    client: AtomicCounterClient | null;
    count: number;
    timeoutHandle: NodeJS.Timeout | null;
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = { client: null, count: 0, timeoutHandle: null };
    }

    public render() {
        const callback = (value: any) => {
            getAuthToken(value).then(t => this.setState({ client: new AtomicCounterClient(t) }));
        };

        const inc = this.increment.bind(this);
        const reset = this.reset.bind(this);

        return (
            <div className="App">
                <header className="App-header">
                    <img src={logo} className="App-logo" alt="logo" />
                    <h1 className="App-title">Atomic Counter</h1>
                </header>
                {this.state.client ? <Tenant client={this.state.client} /> : null}
                <p className="App-intro">
                    {this.state.count}
                </p>
                {this.state.client ? null : <GoogleLogin tokenCallback={callback} />}
                {this.state.client ? <button onClick={inc}>Increment</button> : null}
                {this.state.client ? <button onClick={reset}>Reset</button> : null}
            </div>
        );
    }

    private async increment(): Promise<void> {
        if (this.state.client) {
            try {
                await this.state.client.createCounter("bill", "bill", "bill");
            } catch {
                //
            }

            await this.state.client.increment("bill", "bill", "bill");
        }
    }

    private async reset(): Promise<void> {
        if (this.state.client) {
            await this.state.client.reset("bill", "bill", "bill");
        }
    }
}

export default App;
