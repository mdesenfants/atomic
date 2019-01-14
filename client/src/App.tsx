import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';
import { AtomicCounterClient, getAuthToken } from './atomic-counter/build/dist/atomicCounter';
import GoogleLogin from './GoogleLogin';

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

    public componentDidMount() {
        const handle = setInterval((() => {
            if (this.state.client) {
                this.state.client
                    .count("bill", "bill", "bill")
                    .then(r => this.setState({ count: r }));
            }
        }).bind(this), 1 * 1000);

        this.setState({ timeoutHandle: handle });
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
                <p className="App-intro">
                    {this.state.client ? "logged in" : null}
                </p>
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
            this.state.client.increment("bill", "bill", "bill");
        }
    }

    private async reset(): Promise<void> {
        if (this.state.client) {
            this.state.client.reset("bill", "bill", "bill");
        }
    }
}

export default App;
